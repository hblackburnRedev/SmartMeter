using SmartMeter.Server.Models;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace SmartMeter.Server;

public class Program
{
    private static string _apiKeyFromConfig = "";
    private static readonly ConcurrentDictionary<string, string> _activeSessions = new();

    static async Task Main(string[] args)
    {
        // Load configuration from appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        _apiKeyFromConfig = config["ServerConfig:ApiKey"];

        Console.WriteLine($"Loaded API Key: {_apiKeyFromConfig}");
        try
        {
            await StartServer("127.0.0.1", 8080);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }

    public static async Task StartServer(string ipAddress, int port)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://{ipAddress}:{port}/");
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
    private static async Task ProcessWebSocketRequest(HttpListenerContext context)
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

        if (payload == null || string.IsNullOrWhiteSpace(payload.ClientID) || payload.APIKey != _apiKeyFromConfig)
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
