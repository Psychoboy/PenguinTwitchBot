using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace DotNetTwitchBot.Bot.Notifications
{
    public class WebSocketMessenger : IWebSocketMessenger
    {
        private readonly BlockingCollection<string> _queue = new();
        private List<SocketConnection> websocketConnections = new();
        readonly ILogger<WebSocketMessenger> _logger;
        private bool Paused = false;
        static readonly SemaphoreSlim _semaphoreSlim = new(1);

        public WebSocketMessenger(ILogger<WebSocketMessenger> logger)
        {
            _logger = logger;
            SetupCleanUpTask();
        }

        public void AddToQueue(string message)
        {
            if (Paused) return;
            _queue.Add(message);

        }

        public async Task Handle(Guid id, WebSocket webSocket)
        {
            _logger.LogInformation("Adding Websocket: {id}", id.ToString());
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

            while (webSocket.State == WebSocketState.Open)
            {

                if (_queue.TryTake(out var result, 5000))
                {
                    if (Paused == false)
                    {
                        await SendMessageToSockets(result);
                    }
                }
            }
            _logger.LogInformation("Websocket closed: {id}", id.ToString());
        }

        public async Task CloseAllSockets()
        {
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
        }

        private async Task SendMessageToSockets(string message)
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
                    var bytes = Encoding.Default.GetBytes(message);
                    var arraySegment = new ArraySegment<byte>(bytes);
                    await websocketConnection.WebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            });
            await Task.WhenAll(tasks);
        }

        private void SetupCleanUpTask()
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
                        _logger.LogInformation("Closing Socket: {id}", closedWebsocketConnection.Id);
                    }

                    await Task.Delay(5000);
                }

            });
        }

        public void Pause()
        {
            Paused = true;
            _queue.Clear();
        }

        public void Resume()
        {
            Paused = false;
        }

        public bool IsPaused { get { return Paused; } }
    }

    public class SocketConnection
    {
        public Guid Id { get; set; }
        public WebSocket WebSocket { get; set; } = null!;
    }
}