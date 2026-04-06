using DotNetTwitchBot.Extensions;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.WebSocketEvents
{
    public class WsEventHandler : IWsEventHandler
    {
        private readonly BlockingCollection<WsEvent> queue = [];
        private List<SocketConnection> websocketConnections = [];
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        private readonly ILogger<WsEventHandler> logger;
        private bool paused = false;
        private int maxQueueSize = 100;
        private JsonSerializerOptions serializerOptions;

        public WsEventHandler(ILogger<WsEventHandler> logger)
        {
            this.logger = logger;
            SetupCleanupTasks();
            serializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        }

        public async Task AddToQueue(WsEvent evt)
        {
            if (paused) return;
            try
            {
                await _semaphoreSlim.WaitAsync();
                if (websocketConnections.Count == 0)
                {
                    logger.LogInformation("No WebSocket Connections, skipping event");
                    return;
                }

                if (queue.Count > maxQueueSize)
                {
                    logger.LogInformation("Queue is full, skipping event");
                    return;
                }

                queue.Add(evt);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public async Task Handle(Guid id, WebSocket webSocket)
        {
            logger.LogInformation("Adding Websocket: {id}", id.ToString());
            try
            {
                await _semaphoreSlim.WaitAsync();
                websocketConnections.Add(new SocketConnection
                {
                    Id = id,
                    WebSocket = webSocket
                });
            }
            finally { _semaphoreSlim.Release(); }
            var pushTask = Task.Run(() => PushMessages(webSocket));
            try
            {
                await ReceiveMessage(webSocket);
            }
            catch (Exception)
            {
                logger.LogDebug("Exception thrown in websocket messenger. This is expected when closing.");
            }
            await pushTask;
            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            }
            catch (Exception)
            {
                logger.LogDebug("Exception thrown in websocket messenger. This is expected when closing.");
            }
            logger.LogInformation("Websocket closed: {id}", id.ToString());
        }

        private async Task PushMessages(WebSocket webSocket)
        {

            while (webSocket.State == WebSocketState.Open)
            {
                if (queue.TryTake(out var result, 5000))
                {
                    if (paused == false)
                    {
                        await SendMessageToSockets(result);

                    }
                }
            }
        }

        private async Task ReceiveMessage(WebSocket webSocket)
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var data = await ReadStringAsync(webSocket, CancellationToken.None);
                if (data == null) continue;
                if (data.Length > 0 && data.Equals("pong"))
                {
                    continue;
                }
            }
        }

        private static async Task<string?> ReadStringAsync(WebSocket ws, CancellationToken ct = default)
        {
            var buffer = new ArraySegment<byte>(new byte[1024 * 8]);

            using MemoryStream ms = new();
            WebSocketReceiveResult receiveResult;

            do
            {
                ct.ThrowIfCancellationRequested();

                receiveResult = await ws.ReceiveAsync(buffer, ct);
                if (buffer.Array == null) return null;
                ms.Write(buffer.Array, buffer.Offset, receiveResult.Count);

            } while (!receiveResult.EndOfMessage);


            ms.Seek(0, SeekOrigin.Begin); // Changing stream position to cover whole message


            if (receiveResult.MessageType != WebSocketMessageType.Text)
                return null;

            using StreamReader reader = new(ms, System.Text.Encoding.UTF8);
            return await reader.ReadToEndAsync(ct); // decoding message
        }

        public async Task CloseAllSockets()
        {
            logger.LogDebug("Closing all sockets");
            IEnumerable<SocketConnection> sockets;

            try
            {
                await _semaphoreSlim.WaitAsync();
                sockets = websocketConnections.Where(x => x.WebSocket.State == WebSocketState.Open || x.WebSocket.State == WebSocketState.Connecting);
            }
            finally { _semaphoreSlim.Release(); }
            foreach (var socket in sockets)
            {
                await socket.WebSocket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, String.Empty, CancellationToken.None);
            }
            logger.LogDebug("Closed all sockets");
        }

        public void Pause()
        {
            paused = true;
            queue.Clear();
        }

        public void Resume()
        {
            paused = false;
        }

        private async Task SendMessageToSockets(WsEvent message)
        {
            IEnumerable<SocketConnection> toSentTo;

            try
            {
                await _semaphoreSlim.WaitAsync();
                toSentTo = websocketConnections.ToList();
            }
            finally { _semaphoreSlim.Release(); }

            var tasks = toSentTo.Select(async websocketConnection =>
            {
                if (websocketConnection.WebSocket.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, serializerOptions));
                    var arraySegment = new ArraySegment<byte>(bytes);
                    await websocketConnection.WebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            });
            await Task.WhenAll(tasks);
        }

        private void SetupCleanupTasks()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    IEnumerable<SocketConnection> openSockets;
                    IEnumerable<SocketConnection> closedSockets;

                    try
                    {
                        _semaphoreSlim.Wait();
                        openSockets = websocketConnections.Where(x => x.WebSocket.State == WebSocketState.Open || x.WebSocket.State == WebSocketState.Connecting);
                        closedSockets = websocketConnections.Where(x => x.WebSocket.State != WebSocketState.Open && x.WebSocket.State != WebSocketState.Connecting);

                        websocketConnections = openSockets.ToList();
                    }
                    finally { _semaphoreSlim.Release(); }

                    foreach (var closedWebsocketConnection in closedSockets)
                    {
                        logger.LogInformation("Closing Socket: {id}", closedWebsocketConnection.Id);
                    }

                    await Task.Delay(5000);
                }

            });
        }
    }
    public class SocketConnection
    {
        public Guid Id { get; set; }
        public WebSocket WebSocket { get; set; } = null!;
        public List<string> SubscribedEventTypes { get; set; } = []; //ignored for now, future enhancement to allow clients to subscribe to specific event types
    }
}
