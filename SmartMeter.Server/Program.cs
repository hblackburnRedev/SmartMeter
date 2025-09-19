using System.Net;
using System.Net.WebSockets;

namespace SmartMeter.Server;

    public class Program
    {

        private static CancellationTokenSource _cts = new CancellationTokenSource();

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

                    if (context.Request.IsWebSocketRequest)
                    {
                        await ProcessWebSocketRequest(context);
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

        private static async Task ProcessWebSocketRequest(HttpListenerContext context)
        {
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
    } 
