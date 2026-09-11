using PenguinTwitchBot.Extensions;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace PenguinTwitchBot.Bot.WebSocketEvents
{
    public class WsEventHandler : IWsEventHandler
    {
        private readonly BlockingCollection<WsEvent> queue = [];
        private List<SocketConnection> websocketConnections = [];
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        private readonly ILogger<WsEventHandler> logger;
        private bool paused = false;
        private int maxQueueSize = 1000;
        private int perSocketQueueCapacity = 100;
        private JsonSerializerOptions serializerOptions;
        private readonly CancellationTokenSource _shutdownCts = new();
        private Task? _broadcastTask;
        public WsEventHandler(ILogger<WsEventHandler> logger)
        {
            this.logger = logger;
            SetupCleanupTasks();
            SetupBroadcastTask();
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
                    logger.LogDebug("No WebSocket Connections, skipping event");
                    return;
                }

                if (queue.Count >= maxQueueSize)
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

        public async Task Handle(Guid id, WebSocket webSocket, string? displayName = null)
        {
            logger.LogInformation("Adding Websocket: {id}", id.ToString());
            var connection = new SocketConnection
            {
                Id = id,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName,
                WebSocket = webSocket,
                SenderCancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token),
                OutboundMessages = Channel.CreateBounded<string>(new BoundedChannelOptions(Volatile.Read(ref perSocketQueueCapacity))
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true
                })
            };
            try
            {
                await _semaphoreSlim.WaitAsync();
                websocketConnections.Add(connection);
            }
            finally { _semaphoreSlim.Release(); }

            connection.SendTask = Task.Run(() => SendMessagesAsync(connection, connection.SenderCancellation.Token));
            try
            {
                await ReceiveMessage(webSocket, _shutdownCts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Websocket operation cancelled during shutdown.");
            }
            catch (Exception)
            {
                logger.LogDebug("Exception thrown in websocket messenger. This is expected when closing.");
            }
            await RemoveSocketsById([connection.Id]);
            if (connection.SendTask != null)
                await connection.SendTask;
            connection.SenderCancellation.Dispose();
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

        public async Task<WsEventHandlerStatus> GetStatusAsync()
        {
            try
            {
                await _semaphoreSlim.WaitAsync();
                return new WsEventHandlerStatus(
                    queue.Count,
                    Volatile.Read(ref maxQueueSize),
                    Volatile.Read(ref perSocketQueueCapacity),
                    websocketConnections.Select(connection => new WsSocketStatus(
                        connection.Id,
                        connection.DisplayName,
                        connection.WebSocket.State,
                        Volatile.Read(ref connection.SentMessages),
                        Volatile.Read(ref connection.DroppedMessages),
                        Volatile.Read(ref connection.PeakPendingMessages))).ToList());
            }
            finally { _semaphoreSlim.Release(); }
        }

        public async Task ApplySettingsAsync(int newMaxQueueSize, int newPerSocketQueueCapacity)
        {
            if (newMaxQueueSize is < 1 or > 100_000)
                throw new ArgumentOutOfRangeException(nameof(newMaxQueueSize), "Global queue capacity must be between 1 and 100,000.");
            if (newPerSocketQueueCapacity is < 1 or > 10_000)
                throw new ArgumentOutOfRangeException(nameof(newPerSocketQueueCapacity), "Per-socket queue capacity must be between 1 and 10,000.");

            Volatile.Write(ref maxQueueSize, newMaxQueueSize);
            Volatile.Write(ref perSocketQueueCapacity, newPerSocketQueueCapacity);
            await ReconnectAllSocketsAsync();
        }

        public void ClearGlobalQueue() => queue.Clear();

        public async Task<bool> ReconnectSocketAsync(Guid id)
        {
            SocketConnection? connection;
            try
            {
                await _semaphoreSlim.WaitAsync();
                connection = websocketConnections.FirstOrDefault(item => item.Id == id);
            }
            finally { _semaphoreSlim.Release(); }

            if (connection is null) return false;

            try
            {
                await connection.WebSocket.CloseOutputAsync(
                    WebSocketCloseStatus.EndpointUnavailable,
                    "Reconnect requested",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error requesting websocket reconnect for {id}", id);
            }
            await RemoveSocketsById([id]);
            return true;
        }

        public async Task<int> ReconnectAllSocketsAsync()
        {
            List<SocketConnection> connections;
            try
            {
                await _semaphoreSlim.WaitAsync();
                connections = websocketConnections.ToList();
            }
            finally { _semaphoreSlim.Release(); }

            foreach (var connection in connections)
            {
                try
                {
                    await connection.WebSocket.CloseOutputAsync(
                        WebSocketCloseStatus.EndpointUnavailable,
                        "Websocket settings changed",
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Error requesting websocket reconnect for {id}", connection.Id);
                }
            }

            await RemoveSocketsById(connections.Select(connection => connection.Id).ToHashSet());
            return connections.Count;
        }

        private void SetupBroadcastTask()
        {
            _broadcastTask = Task.Run(() => RunBroadcastLoopAsync(_shutdownCts.Token));
        }

        private async Task RunBroadcastLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (queue.TryTake(out var result, 5000, cancellationToken))
                    {
                        if (!paused)
                        {
                            await SendMessageToSockets(result);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Broadcast task cancelled for shutdown.");
            }
        }

        private async Task ReceiveMessage(WebSocket webSocket, CancellationToken cancellationToken)
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var data = await ReadStringAsync(webSocket, cancellationToken);
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

            // Signal shutdown to all operations
            _shutdownCts.Cancel();

            IEnumerable<SocketConnection> sockets;

            try
            {
                await _semaphoreSlim.WaitAsync();
                sockets = websocketConnections.Where(x => x.WebSocket.State == WebSocketState.Open || x.WebSocket.State == WebSocketState.Connecting);
            }
            finally { _semaphoreSlim.Release(); }

            var closeTasks = sockets.Select(async socket =>
            {
                try
                {
                    await socket.WebSocket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, String.Empty, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Error closing socket {id}", socket.Id);
                }
            });

            await Task.WhenAll(closeTasks);
            if (_broadcastTask != null)
                await _broadcastTask;
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

            var serializedMessage = JsonSerializer.Serialize(message, serializerOptions);
            foreach (var websocketConnection in toSentTo)
            {
                if (websocketConnection.WebSocket.State == WebSocketState.Open)
                {
                    var pendingMessages = Interlocked.Increment(ref websocketConnection.PendingMessages);
                    UpdatePeak(ref websocketConnection.PeakPendingMessages, pendingMessages);
                    if (!websocketConnection.OutboundMessages.Writer.TryWrite(serializedMessage))
                    {
                        Interlocked.Decrement(ref websocketConnection.PendingMessages);
                        Interlocked.Increment(ref websocketConnection.DroppedMessages);
                        logger.LogWarning("Outbound websocket queue is full for {id}; dropping event.", websocketConnection.Id);
                    }
                }
            }
        }

        private async Task SendMessagesAsync(SocketConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var message in connection.OutboundMessages.Reader.ReadAllAsync(cancellationToken))
                {
                    Interlocked.Decrement(ref connection.PendingMessages);
                    var bytes = Encoding.UTF8.GetBytes(message);
                    await connection.WebSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
                    Interlocked.Increment(ref connection.SentMessages);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug("Websocket sender cancelled for {id}.", connection.Id);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error sending websocket event to {id}", connection.Id);
                await RemoveSocketsById([connection.Id]);
            }
        }

        private static void UpdatePeak(ref int peak, int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref peak);
                if (value <= current) return;
            }
            while (Interlocked.CompareExchange(ref peak, value, current) != current);
        }

        private void SetupCleanupTasks()
        {
            Task.Run(async () =>
            {
                while (! _shutdownCts.IsCancellationRequested)
                {
                    List<SocketConnection> closedSockets;

                    try
                    {
                        _semaphoreSlim.Wait();
                        var openSockets = websocketConnections.Where(x => x.WebSocket.State == WebSocketState.Open || x.WebSocket.State == WebSocketState.Connecting).ToList();
                        closedSockets = websocketConnections.Where(x => x.WebSocket.State != WebSocketState.Open && x.WebSocket.State != WebSocketState.Connecting).ToList();

                        foreach (var socket in closedSockets)
                        {
                            socket.SenderCancellation.Cancel();
                            socket.OutboundMessages.Writer.TryComplete();
                        }

                        websocketConnections = openSockets;
                        if (!openSockets.Any())
                            queue.Clear();
                    }
                    finally { _semaphoreSlim.Release(); }

                    foreach (var closedWebsocketConnection in closedSockets)
                    {
                        logger.LogInformation("Closing Socket: {id}", closedWebsocketConnection.Id);
                    }

                    await Task.Delay(5000, _shutdownCts.Token);
                }

            });
        }

        private async Task RemoveSocketsById(HashSet<Guid> socketIds)
        {
            List<SocketConnection> removedSockets;
            bool noConnections;
            try
            {
                await _semaphoreSlim.WaitAsync();
                removedSockets = websocketConnections.Where(x => socketIds.Contains(x.Id)).ToList();
                websocketConnections = websocketConnections.Where(x => !socketIds.Contains(x.Id)).ToList();
                noConnections = websocketConnections.Count == 0;

                foreach (var socket in removedSockets)
                {
                    socket.SenderCancellation.Cancel();
                    socket.OutboundMessages.Writer.TryComplete();
                }
            }
            finally { _semaphoreSlim.Release(); }

            if (noConnections)
                queue.Clear();

        }
    }
    public class SocketConnection
    {
        public Guid Id { get; set; }
        public string DisplayName { get; set; } = "Unknown";
        public int PendingMessages;
        public int SentMessages;
        public int DroppedMessages;
        public int PeakPendingMessages;
        public WebSocket WebSocket { get; set; } = null!;
        public Channel<string> OutboundMessages { get; set; } = null!;
        public Task? SendTask { get; set; }
        public CancellationTokenSource SenderCancellation { get; set; } = null!;
        public List<string> SubscribedEventTypes { get; set; } = []; //ignored for now, future enhancement to allow clients to subscribe to specific event types
    }
}
