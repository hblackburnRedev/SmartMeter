using System.Collections.Concurrent;
using SmartMeter.Server.Services.Abstractions;
using SmartMeter.Server.Contracts;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartMeter.Server.Configuration;

namespace SmartMeter.Server.Services;

public class WebSocketServer(IOptions<ServerConfiguration> config) : IWebSocketServer
{
    private readonly ConcurrentDictionary<string, string> _activeSessions = new();
    
    public async Task StartServer()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://{config.Value.IpAddress}:{config.Value.Port}/");
        listener.Start();

        try
        {
            Console.WriteLine("Server started. Waiting for connections...");

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();

                // Handle authentication 
                if (context.Request.IsWebSocketRequest)
                {
                    await ProcessWebSocketRequest(context);
                }
                else
                {
                    // Reject HTTP requests
                    context.Response.StatusCode = 400;
                    byte[] msg = Encoding.UTF8.GetBytes("This server only supports WebSocket connections.");
                    await context.Response.OutputStream.WriteAsync(msg, 0, msg.Length);
                    context.Response.Close();
                }

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine("Server stopped.");

            listener.Stop();
        }
    }
    private async Task ProcessWebSocketRequest(HttpListenerContext context)
    {
        HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
        WebSocket socket = webSocketContext.WebSocket;

        byte[] buffer = new byte[1024];

        // Expect first message to be authentication payload
        WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType != WebSocketMessageType.Text)
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Expected text message for authentication", CancellationToken.None);
            return;
        }

        string authMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);

        AuthRequest? payload = null;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            payload = JsonSerializer.Deserialize<AuthRequest>(authMessage, options);
        }
        catch
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid authentication format", CancellationToken.None);
            return;
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.ClientID) || payload.APIKey != config.Value.ApiKey)
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Authentication failed", CancellationToken.None);
            return;
        }

        var sessionKey = Guid.NewGuid().ToString();
        _activeSessions[sessionKey] = payload.ClientID;

        // Optionally, send sessionKey to client
        var response = new { success = true, message = "Authentication successful", sessionKey };
        var responseJson = JsonSerializer.Serialize(response);
        await socket.SendAsync(Encoding.UTF8.GetBytes(responseJson), WebSocketMessageType.Text, true, CancellationToken.None);

        Console.WriteLine($"Client {payload.ClientID} authenticated and connected.");

        // Now handle further messages
        while (socket.State == WebSocketState.Open)
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"Received message: {receivedMessage}");

                // Echo back the received message
                await socket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
        }
    }
}