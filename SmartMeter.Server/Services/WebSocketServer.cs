using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Contracts;
using SmartMeter.Server.Helpers;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.Server.Services;

public class WebSocketServer(
    ILogger<WebSocketServer> logger,
    IOptions<ServerConfiguration> config,
    IClientService clientService,
    IPricingService pricingService) 
    : BackgroundService
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    
    private const string ClientIdHeaderName = "ClientId";
    private const string ApiKeyHeaderName = "ApiKey";
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true, 
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };


    private async Task BroadcastGridAlertAsync(
        ElectricityGridStatusResponse alert,
        CancellationToken ct = default)
    {
        
        var json = JsonSerializer.Serialize(alert, _jsonOptions);
        
        foreach (var socket in _sockets.Values)
        {
            if (socket.State != WebSocketState.Open)
                continue;

            try
            {
                await socket.SendAsync(
                    Encoding.UTF8.GetBytes(json),
                    WebSocketMessageType.Text,
                    true,
                    ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send alert to a client");
            }
        }
    }
    
    private async Task SimulateGridAsync(CancellationToken ct = default)
    {
        var random = new Random();

        while (!ct.IsCancellationRequested)
        {
            var delayUntilNextOutage = TimeSpan.FromSeconds(random.Next(10, 30));
            await Task.Delay(delayUntilNextOutage, ct);

            logger.LogWarning("Simulated: electricity grid DOWN");
            await BroadcastGridAlertAsync(
                new ElectricityGridStatusResponse
                {
                    Status = "down"
                },
                ct);

            var outageDuration = TimeSpan.FromSeconds(random.Next(5, 10));
            await Task.Delay(outageDuration, ct);

            logger.LogInformation("Simulated: electricity grid UP");
            await BroadcastGridAlertAsync(
                new ElectricityGridStatusResponse
                {
                    Status = "Up"
                },
                ct);        
        }
    }


    private async Task ProcessWebSocketRequest(
        HttpListenerContext context,
        string clientId,
        CancellationToken ct =  default)
    {
        var clientAddress = context.Request.RemoteEndPoint.Address.ToString();
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
            _sockets[sessionKey] =  socket;

            logger.LogInformation(
                "Client {ClientID} authenticated successfully from {ClientAddress} with session {SessionKey}",
                clientId, clientAddress, sessionKey);

            var buffer = new byte[1024];

            var client = await clientService.GetSmartMeterClientAsync(Guid.Parse(clientId));
            
            while (socket.State == WebSocketState.Open)
            {
                var newClient = client is null;

                var message = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (message.MessageType == WebSocketMessageType.Text)
                {
                    var messageAsString = Encoding.UTF8.GetString(buffer, 0, message.Count);
                    logger.LogInformation("Message received from {ClientID}: {Message}", clientId, messageAsString);

                    string response;
                    
                    if (newClient)
                    {
                        if (!JsonDeserializerHelper.TryDeserialize(messageAsString, _jsonOptions, out NewClientRequest? newClientRequest) ||
                            newClientRequest is null)
                        {
                            logger.LogWarning("Invalid message format from {ClientAddress}", clientAddress);
                            closeStatus = WebSocketCloseStatus.InvalidPayloadData;
                            closeDescription = "Invalid message format";
                            break;
                        }
                        
                        client = await clientService.AddSmartMeterClientAsync(
                            Guid.Parse(clientId),
                            newClientRequest.ClientName,
                            newClientRequest.Address);

                        response =
                            JsonSerializer.Serialize(client,
                                _jsonOptions);
                    }
                    else
                    {
                        if (!JsonDeserializerHelper.TryDeserialize(messageAsString, _jsonOptions, out ReadingRequest? readingRequest) ||
                            readingRequest is null)
                        {
                            logger.LogWarning("Invalid message format from {ClientAddress}", clientAddress);
                            closeStatus = WebSocketCloseStatus.InvalidPayloadData;
                            closeDescription = "Invalid message format";
                            break;
                        }

                        var pricing = await pricingService.CalculatePriceAsync(readingRequest.Region, readingRequest.Usage, clientId);

                        response = JsonSerializer.Serialize(new ReadingResponse 
                        {   
                            Region =  readingRequest.Region,
                            Usage = readingRequest.Usage,
                            Price = pricing,
                        });
                    }
                    
                    await socket.SendAsync(
                        Encoding.UTF8.GetBytes(response), 
                        WebSocketMessageType.Text,
                        true,
                        ct);
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
            
            closeStatus = WebSocketCloseStatus.InternalServerError;
            closeDescription = $"Server encountered an internal error: {ex.Message}";
        }
        finally
        {
            if (socket is not null)
            {
                var sessionToRemove = _sockets
                    .FirstOrDefault(kvp => kvp.Value == socket).Key;

                if (sessionToRemove is not null)
                {
                    _sockets.TryRemove(sessionToRemove, out _);
                }
                
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


            if (config.Value.EnableGridAlerts)
            {
                _ = SimulateGridAsync(stoppingToken);

            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(stoppingToken);

                // Support both query parameters (for browser WebSocket) and headers
                var clientId = context.Request.QueryString["clientId"]
                               ?? context.Request.Headers[ClientIdHeaderName];
                
                var apiKey = context.Request.QueryString["apiKey"]
                             ?? context.Request.Headers[ApiKeyHeaderName];
                
                if (!context.Request.IsWebSocketRequest)
                {
                    logger.LogWarning("Rejected non-WebSocket request from {RemoteIP}",
                        context.Request.RemoteEndPoint?.Address);

                    context.Response.StatusCode = 400;
                    var msg = "This server only supports WebSocket connections."u8.ToArray();
                    await context.Response.OutputStream.WriteAsync(msg, stoppingToken);
                    context.Response.Close();

                    continue;
                }

                if (string.IsNullOrWhiteSpace(apiKey)|| string.IsNullOrWhiteSpace(clientId))
                {
                    context.Response.StatusCode = 401;
                    var msg = "Unauthorized: invalid credentials."u8.ToArray();
                    await context.Response.OutputStream.WriteAsync(msg, stoppingToken);
                    context.Response.Close();

                    logger.LogWarning("Unauthorized connection attempt - missing credentials");

                    continue;
                }

                if (apiKey != config.Value.ApiKey)
                {
                    // Validate API key matches configuration
                    context.Response.StatusCode = 401;
                    var msg = "Unauthorized: invalid API key."u8.ToArray();
                    await context.Response.OutputStream.WriteAsync(msg, stoppingToken);
                    context.Response.Close();

                    logger.LogWarning("Unauthorized connection attempt - invalid API key");

                    continue;
                }

                await ProcessWebSocketRequest(context, clientId, stoppingToken);
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