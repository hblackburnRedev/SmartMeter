using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace SmartMeter.Server;

public class Program
{

    private static CancellationTokenSource _cts = new CancellationTokenSource();
    private static readonly Dictionary<string, string> _validClients = new()
    {
        { "client-01", "meterG76" },
        { "client-02", "meterO06" },
        { "client-03", "meterS87" },
        { "client-04", "meterA12" },
        { "client-05", "meterB34" },
        { "client-06", "meterC56" },
        { "client-07", "meterD78" },
        { "client-08", "meterE90" },
        { "client-09", "meterF12" },
        { "client-10", "meterG34" },
        { "client-11", "meterH56" },
        { "client-12", "meterI78" }
    };

    private static readonly Dictionary<string, string> _activeSessions = new();

    static async Task Main(string[] args)
    {
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
                    context.Response.Close();
                }
                else
                {
                    context.Response.StatusCode = 400;
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

    private static async Task HandleAuthRequest(HttpListenerContext context)
    {
        try
        {
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            string body = await reader.ReadToEndAsync();

            var payload = JsonSerializer.Deserialize<AuthRequest>(body);

            if (payload == null || string.IsNullOrWhiteSpace(payload.ClientID) || string.IsNullOrWhiteSpace(payload.APIKey))
            {
                await WriteJsonResponse(context, new { success = false, message = "Invalid request data" }, HttpStatusCode.BadRequest);
                return;
            }

            // Validate credentials
            if (_validClients.TryGetValue(payload.ClientID, out var expectedKey) && expectedKey == payload.APIKey)
            {
                var sessionKey = Guid.NewGuid().ToString(); 
                _activeSessions[sessionKey] = payload.ClientID; // store active session

                await WriteJsonResponse(context, new
                {
                    success = true,
                    message = "Authentication successful",
                    sessionKey
                });
            }
            else
            {
                await WriteJsonResponse(context, new
                {
                    success = false,
                    message = "Authentication failed"
                }, HttpStatusCode.Unauthorized);
            }
        }
        catch (Exception ex)
        {
            await WriteJsonResponse(context, new { success = false, message = ex.Message }, HttpStatusCode.InternalServerError);
        }
    }
    private static async Task ProcessWebSocketRequest(HttpListenerContext context)
    {
        string? key = context.Request.QueryString["sessionKey"];

        if (string.IsNullOrEmpty(key) || !_activeSessions.ContainsKey(key))
        {
            context.Response.StatusCode = 403;
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Invalid or missing session key"));
            context.Response.Close();
            return;
        }

        var clientId = _activeSessions[key];
        Console.WriteLine($"Client {clientId} connected with valid session key.");

        HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
        WebSocket socket = webSocketContext.WebSocket;

        // Handle incoming messages
        byte[] buffer = new byte[1024];

        while (socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                string receivedMessage = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
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
    private static async Task WriteJsonResponse(HttpListenerContext context, object obj, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        string json = JsonSerializer.Serialize(obj);
        byte[] data = Encoding.UTF8.GetBytes(json);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.OutputStream.WriteAsync(data, 0, data.Length);
        context.Response.Close();
    }
    private class AuthRequest
    {
        public string ClientID { get; set; } = "";
        public string APIKey { get; set; } = "";
    }
}
