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
        try
        {
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            socket = webSocketContext.WebSocket;
            logger.LogDebug("WebSocket accepted for {ClientAddress}", clientAddress);

            byte[] buffer = new byte[1024];
            
            var sessionKey = Guid.NewGuid().ToString();
            _activeSessions[sessionKey] = clientId;

            logger.LogInformation("Client {ClientID} authenticated successfully from {ClientAddress} with session {SessionKey}", 
                clientId, clientAddress, sessionKey);
            
            // Message handling loop
            while (socket.State == WebSocketState.Open)
            {
                var message = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (message.MessageType == WebSocketMessageType.Text)
                {
                    string messageAsString = Encoding.UTF8.GetString(buffer, 0, message.Count);
                    logger.LogInformation("Message received from {ClientID}: {Message}", clientId, messageAsString);
                    
                    logger.LogInformation("About to deserialize with case insensitive: {CaseInsensitive}", _jsonOptions.PropertyNameCaseInsensitive);

                    if (JsonSerializer.Deserialize<ReadingRequest>(messageAsString, _jsonOptions) is not ReadingRequest payload)
                    {
                        logger.LogWarning("Invalid message format from {ClientAddress}", clientAddress);
                        await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid message format", ct);
                        return;
                    }

                    var pricing = await pricingService.CalculatePriceAsync(payload.Region, payload.Usage, clientId);
                    
                    var response = new ReadingResponse(payload.Region, payload.Usage, pricing);
                    
                    var responseAsJson = JsonSerializer.Serialize(response, _jsonOptions);

                    // Send calculated price back to client
                    await socket.SendAsync(
                        Encoding.UTF8.GetBytes(responseAsJson), 
                        WebSocketMessageType.Text, 
                        true, 
                        ct);
                }
                else if (message.MessageType == WebSocketMessageType.Close)
                {
                    logger.LogInformation("Client {ClientID} disconnected normally.", clientId);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while handling WebSocket connection from {ClientAddress}", clientAddress);

            if (socket is not null)
            {
                await socket.SendAsync(
                    "An error has occured"u8.ToArray(), 
                    WebSocketMessageType.Text, 
                    true, 
                    CancellationToken.None);
            }
        }
        finally
        {
            if (socket?.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", ct);
            }
            
            socket?.Dispose();
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
                HttpListenerContext context = await listener.GetContextAsync();
                
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
                    
                    logger.LogWarning("Unauthorized connection attempt - invalid API key from client {ClientId}", clientId);
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