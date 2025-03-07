using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DotNetTwitchBot.Application.TTS;
using DotNetTwitchBot.Application.WheelSpinNotifications;
using DotNetTwitchBot.Extensions;
using MediatR;

namespace DotNetTwitchBot.Bot.Notifications
{
    public class WebSocketMessenger : IWebSocketMessenger
    {
        private readonly BlockingCollection<string> _queue = [];
        private List<SocketConnection> websocketConnections = [];
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        readonly ILogger<WebSocketMessenger> _logger;
        private bool Paused = false;
        //static readonly SemaphoreSlim _semaphoreSlim = new(1);
        private readonly IMediator _mediator;

        public WebSocketMessenger(ILogger<WebSocketMessenger> logger, IMediator mediator)
        {
            _logger = logger;
            SetupCleanUpTask();
            _mediator = mediator;
        }

        public async Task AddToQueue(string message)
        {
            if (Paused) return;
            try
            {
                await _semaphoreSlim.WaitAsync();
                if(websocketConnections.Count == 0)
                {
                    _logger.LogWarning("No websockets connected. Not adding message to queue.");
                    return;
                }
                if (_queue.Count > 100)
                {
                    _logger.LogWarning("Queue is full. Not adding message to queue.");
                    return;
                }
                _queue.Add(message);

            }
            finally { _semaphoreSlim.Release(); }
            

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

            var pushTask = Task.Run(() => PushMessages(webSocket));
            try
            {
                await ReceiveMessage(webSocket);
            }
            catch (Exception)
            {
                _logger.LogDebug("Exception thrown in websocket messenger. This is expected when closing.");
            }
            await pushTask;
            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            }
            catch (Exception)
            {
                _logger.LogDebug("Exception thrown in websocket messenger. This is expected when closing.");
            }
            _logger.LogInformation("Websocket closed: {id}", id.ToString());
        }

        private async Task PushMessages(WebSocket webSocket)
        {

            while (webSocket.State == WebSocketState.Open)
            {
                if (_queue.TryTake(out var result, 5000))
                {
                    if (Paused == false)
                    {
                        await SendMessageToSockets(result);

                    }
                }
                await SendMessageToSockets("ping");

            }
        }

        private async Task ReceiveMessage(WebSocket webSocket)
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var data = await ReadStringAsync(webSocket, CancellationToken.None);
                if (data == null) continue;
                if(data.Length > 0 && data.Equals("pong"))
                {
                    continue;
                } else if (data.Length >0 && data.StartsWith("TTSComplete: "))
                {
                    await _mediator.Publish(new TTSDeleteNotification(data));
                } else if(data.Length > 0 && data.StartsWith("{\"wheel\":"))
                {
                    var wheelSpinComplete = JsonSerializer.Deserialize<WheelSpinComplete>(data);
                    if (wheelSpinComplete != null)
                        await _mediator.Publish(new WheelSpinCompleteNotification(wheelSpinComplete));
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
            _logger.LogDebug("Closing all sockets");
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
            _logger.LogDebug("Closed all sockets");
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