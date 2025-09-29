using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace SmartMeter.Server;

/// <summary>
/// WebSocket server for handling smart meter client connections.
/// NOTE: This is a proof of concept implementation for testing purposes.
/// </summary>
public class Program
{
    // Thread-safe dictionary to store active client connections
    private static ConcurrentDictionary<string, ClientConnection> _clients = new();
    
    // Current electricity price per kWh (mock value for testing)
    private static decimal _electricityPrice = 0.28m;

    static async Task Main(string[] args)
    {
        try
        {
            await StartServer("127.0.0.1", 8080);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Starts the WebSocket server on the specified IP address and port
    /// </summary>
    public static async Task StartServer(string ipAddress, int port)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://{ipAddress}:{port}/");
        listener.Start();

        Console.WriteLine($"Server started on ws://{ipAddress}:{port}");
        Console.WriteLine($"Electricity price: £{_electricityPrice}/kWh");
        Console.WriteLine("Waiting for client connections...\n");

        try
        {
            // Main server loop - continuously accept new connections
            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();

                // Only accept WebSocket upgrade requests
                if (context.Request.IsWebSocketRequest)
                {
                    // Handle each client connection in a separate task for concurrency
                    _ = Task.Run(async () => await ProcessWebSocketRequest(context));
                }
                else
                {
                    // Reject non-WebSocket requests
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server stopped: {ex.Message}");
            listener.Stop();
        }
    }

    /// <summary>
    /// Processes WebSocket requests from individual clients
    /// </summary>
    private static async Task ProcessWebSocketRequest(HttpListenerContext context)
    {
        // Upgrade HTTP connection to WebSocket
        HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
        WebSocket socket = webSocketContext.WebSocket;
        string? clientId = null;

        try
        {
            byte[] buffer = new byte[4096];

            // Main client communication loop
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Decode received message
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    try
                    {
                        // Parse JSON message and extract type
                        var message = JsonSerializer.Deserialize<JsonElement>(receivedMessage);
                        var messageType = message.GetProperty("type").GetString();

                        // Route message to appropriate handler based on type
                        switch (messageType)
                        {
                            case "auth":
                                clientId = await HandleAuthentication(socket, message);
                                break;

                            case "meter_reading":
                                await HandleMeterReading(socket, message, clientId);
                                break;

                            default:
                                Console.WriteLine($"Unknown message type: {messageType}");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing message: {ex.Message}");
                        await SendError(socket, "Invalid message format");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    // Handle graceful client disconnection
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    
                    if (clientId != null && _clients.TryRemove(clientId, out var client))
                    {
                        Console.WriteLine($"Client disconnected: {clientId}");
                        Console.WriteLine($"Active clients: {_clients.Count}\n");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket error: {ex.Message}");
            
            // Clean up client from dictionary on error
            if (clientId != null)
            {
                _clients.TryRemove(clientId, out _);
            }
        }
    }

    /// <summary>
    /// Handles client authentication requests
    /// NOTE: This is a basic implementation it's not even setup yet anyway
    /// </summary>
    private static async Task<string> HandleAuthentication(WebSocket socket, JsonElement message)
    {
        var meterId = message.GetProperty("meterId").GetString();
        
        if (string.IsNullOrEmpty(meterId))
        {
            await SendError(socket, "Invalid meter ID");
            return null;
        }

        // Create new client connection record
        var client = new ClientConnection
        {
            MeterId = meterId,
            Socket = socket,
            TotalReading = 0,
            TotalBill = 0,
            ConnectedAt = DateTime.UtcNow
        };

        // Store client in concurrent dictionary
        _clients[meterId] = client;

        Console.WriteLine($"New client authenticated: {meterId}");
        Console.WriteLine($"Active clients: {_clients.Count}\n");

        // Send authentication success response
        await SendMessage(socket, new
        {
            type = "auth_success",
            meterId = meterId,
            timestamp = DateTime.UtcNow
        });

        return meterId;
    }

    /// <summary>
    /// Handles meter reading submissions from clients
    /// Calculates bill and sends update back to client
    /// </summary>
    private static async Task HandleMeterReading(WebSocket socket, JsonElement message, string? clientId)
    {
        // Verify client is authenticated
        if (clientId == null)
        {
            await SendError(socket, "Not authenticated");
            return;
        }

        if (!_clients.TryGetValue(clientId, out var client))
        {
            await SendError(socket, "Client not found");
            return;
        }

        // Extract reading data from message
        var reading = message.GetProperty("reading").GetDecimal();
        var timestamp = message.GetProperty("timestamp").GetString();

        // Update client's cumulative reading
        client.TotalReading = reading;
        
        // Calculate bill based on current electricity price
        client.TotalBill = reading * _electricityPrice;

        Console.WriteLine($"Reading from {clientId}: {reading:F3} kWh");
        Console.WriteLine($"Calculated bill: £{client.TotalBill:F2}\n");

        // Acknowledge receipt of reading
        await SendMessage(socket, new
        {
            type = "reading_acknowledged",
            meterId = clientId,
            reading = reading,
            timestamp = DateTime.UtcNow
        });

        // Push updated bill to client
        await SendMessage(socket, new
        {
            type = "bill_update",
            bill = client.TotalBill,
            reading = reading,
            pricePerKwh = _electricityPrice,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Sends a JSON message to a specific client
    /// </summary>
    private static async Task SendMessage(WebSocket socket, object data)
    {
        if (socket.State != WebSocketState.Open)
            return;

        try
        {
            // Serialize object to JSON and convert to bytes
            var json = JsonSerializer.Serialize(data);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            // Send message over WebSocket
            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends an error message to a specific client
    /// </summary>
    private static async Task SendError(WebSocket socket, string errorMessage)
    {
        await SendMessage(socket, new
        {
            type = "error",
            message = errorMessage,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcasts a grid alert message to all connected clients
    /// Demonstrates server push capability
    /// </summary>
    public static async Task BroadcastGridAlert(string message)
    {
        var alert = new
        {
            type = "grid_alert",
            message = message,
            timestamp = DateTime.UtcNow
        };

        // Send alert to all connected clients concurrently
        var tasks = _clients.Values
            .Where(c => c.Socket.State == WebSocketState.Open)
            .Select(c => SendMessage(c.Socket, alert));

        await Task.WhenAll(tasks);
        
        Console.WriteLine($"Broadcast grid alert to {_clients.Count} clients: {message}\n");
    }
}

/// <summary>
/// Represents a connected smart meter client
/// </summary>
public class ClientConnection
{
    public string MeterId { get; set; }
    public WebSocket Socket { get; set; }
    public decimal TotalReading { get; set; }
    public decimal TotalBill { get; set; }
    public DateTime ConnectedAt { get; set; }
}