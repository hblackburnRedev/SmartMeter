using System.Collections.Concurrent;
using SmartMeter.Server.Services.Abstractions;
using SmartMeter.Server.Contracts;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Helpers;

namespace SmartMeter.Server.Services;

public class WebSocketServer(
    ILogger<WebSocketServer> logger,
    IOptions<ServerConfiguration> config,
    IPricingService pricingService) 
    : BackgroundService
{
    private readonly ConcurrentDictionary<string, string> _activeSessions = new();
    
    private const string ClientIdHeaderName = "ClientId";
    private const string ApiKeyHeaderName = "ApiKey";
    
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
    
    private async Task ProcessWebSocketRequest(HttpListenerContext context, string clientId, CancellationToken ct)
    {
        string? clientAddress = context.Request.RemoteEndPoint?.Address.ToString();
        logger.LogInformation("Incoming WebSocket connection from {ClientAddress}", clientAddress);

        WebSocket? socket = null;
        WebSocketCloseStatus? closeStatus = null;
        string? closeDescription = null;

        try
        {
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            socket = webSocketContext.WebSocket;

            logger.LogDebug("WebSocket accepted for {ClientAddress}", clientAddress);

            var sessionKey = Guid.NewGuid().ToString();
            _activeSessions[sessionKey] = clientId;

            logger.LogInformation(
                "Client {ClientID} authenticated successfully from {ClientAddress} with session {SessionKey}",
                clientId, clientAddress, sessionKey);

            byte[] buffer = new byte[1024];

            while (socket.State == WebSocketState.Open)
            {
                var message = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (message.MessageType == WebSocketMessageType.Text)
                {
                    var messageAsString = Encoding.UTF8.GetString(buffer, 0, message.Count);
                    logger.LogInformation("Message received from {ClientID}: {Message}", clientId, messageAsString);
                    
                    if (!JsonDeserializerHelper.TryDeserialize(messageAsString, _jsonOptions, out ReadingRequest? readingRequest) ||
                        readingRequest is null)
                    {
                        logger.LogWarning("Invalid message format from {ClientAddress}", clientAddress);
                        closeStatus = WebSocketCloseStatus.InvalidPayloadData;
                        closeDescription = "Invalid message format";
                        break;
                    }

                    var pricing = await pricingService.CalculatePriceAsync(readingRequest.Region, readingRequest.Usage, clientId);
                    
                    var responseJson =
                        JsonSerializer.Serialize(new ReadingResponse(readingRequest.Region, readingRequest.Usage, pricing),
                            _jsonOptions);

                    await socket.SendAsync(Encoding.UTF8.GetBytes(responseJson),
                        WebSocketMessageType.Text, true, ct);
                }
                else if (message.MessageType == WebSocketMessageType.Close)
                {
                    logger.LogInformation("Client {ClientID} disconnected normally.", clientId);
                    closeStatus = WebSocketCloseStatus.NormalClosure;
                    closeDescription = "Client closed the connection";
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning("Server shutdown");

            closeStatus = WebSocketCloseStatus.EndpointUnavailable;
            closeDescription = "Server shutting down";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while handling WebSocket connection from {ClientAddress}", clientAddress);

            if (socket is not null)
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes(
                    $"Error while handling request: {ex.Message}"),
                    WebSocketMessageType.Text, 
                    true,
                    ct);
            }
            
            closeStatus = WebSocketCloseStatus.InternalServerError;
            closeDescription = "Server encountered an internal error";
        }
        finally
        {
            if (socket is not null)
            {
                try
                {
                    // Only close if still open
                    if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    {
                        await socket.CloseAsync(
                            closeStatus ?? WebSocketCloseStatus.InternalServerError,
                            closeDescription ?? "Connection closed",
                            CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed while closing WebSocket for {ClientAddress}", clientAddress);
                }
                finally
                {
                    socket.Dispose();
                }
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://{config.Value.IpAddress}:{config.Value.Port}/");

        try
        {
            listener.Start();
            logger.LogInformation("WebSocket server started on {Address}:{Port}", 
                config.Value.IpAddress, config.Value.Port);

            while (!stoppingToken.IsCancellationRequested)
            {
                HttpListenerContext context = await listener.GetContextAsync().WaitAsync(stoppingToken);

                // Support both query parameters (for browser WebSocket) and headers
                var clientId = context.Request.QueryString["clientId"]
                               ?? context.Request.Headers[ClientIdHeaderName];
                
                var apiKey = context.Request.QueryString["apiKey"]
                             ?? context.Request.Headers[ApiKeyHeaderName];

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(clientId))
                {
                    context.Response.StatusCode = 401;
                    byte[] msg = Encoding.UTF8.GetBytes("Unauthorized: invalid credentials.");
                    await context.Response.OutputStream.WriteAsync(msg, stoppingToken);
                    context.Response.Close();

                    logger.LogWarning("Unauthorized connection attempt - missing credentials");
                }
                else if (apiKey != config.Value.ApiKey)
                {
                    // Validate API key matches configuration
                    context.Response.StatusCode = 401;
                    byte[] msg = Encoding.UTF8.GetBytes("Unauthorized: invalid API key.");
                    await context.Response.OutputStream.WriteAsync(msg, stoppingToken);
                    context.Response.Close();

                    logger.LogWarning("Unauthorized connection attempt - invalid API key from client {ClientId}",
                        clientId);
                }
                else if (context.Request.IsWebSocketRequest)
                {
                    await ProcessWebSocketRequest(context, clientId, stoppingToken);
                }
                else
                {
                    logger.LogWarning("Rejected non-WebSocket request from {RemoteIP}",
                        context.Request.RemoteEndPoint?.Address);

                    context.Response.StatusCode = 400;
                    byte[] msg = Encoding.UTF8.GetBytes("This server only supports WebSocket connections.");
                    await context.Response.OutputStream.WriteAsync(msg, stoppingToken);
                    context.Response.Close();
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("WebSocket server canceled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error in WebSocket server: {Message}", ex.Message);
        }
        finally
        {
            if (listener.IsListening)
            {
                listener.Stop();
                logger.LogInformation("WebSocket server stopped.");
            }
        }
    }
}