using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using PenguinTwitchBot.Application.TTS;
using PenguinTwitchBot.Application.WheelSpinNotifications;
using PenguinTwitchBot.Extensions;

namespace PenguinTwitchBot.Bot.Notifications
{
    public class WebSocketMessenger : IWebSocketMessenger
    {
        private readonly BlockingCollection<string> _queue = [];
        private List<SocketConnection> websocketConnections = [];
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        readonly ILogger<WebSocketMessenger> _logger;
        private bool Paused = false;
        //static readonly SemaphoreSlim _semaphoreSlim = new(1)  ;
        private readonly Application.Notifications.IPenguinDispatcher _dispatcher;
        private readonly CancellationTokenSource _shutdownCts = new();
        private Task? _broadcastTask;
        private Task? _cleanupTask;
        private static readonly TimeSpan _idlePingInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(5);
        private const int PerSocketQueueCapacity = 100;

        public WebSocketMessenger(ILogger<WebSocketMessenger> logger, Application.Notifications.IPenguinDispatcher dispatcher)
        {
            _logger = logger;
            SetupCleanUpTask();
            SetupBroadcastTask();
            _dispatcher = dispatcher;
        }

        public async Task AddToQueue(string message)
        {
            if (Paused) return;
            try
            {
                await _semaphoreSlim.WaitAsync();
                if(websocketConnections.Count == 0)
                {
                    _logger.LogDebug("No websockets connected. Not adding message to queue.");
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
            var connection = new SocketConnection
            {
                Id = id,
                WebSocket = webSocket,
                SenderCancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token),
                OutboundMessages = Channel.CreateBounded<string>(new BoundedChannelOptions(PerSocketQueueCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropWrite,
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
                _logger.LogDebug("Websocket operation cancelled during shutdown.");
            }
            catch (Exception)
            {
                _logger.LogDebug("Exception thrown in websocket messenger. This is expected when closing.");
            }
            connection.SenderCancellation.Cancel();
            connection.OutboundMessages.Writer.TryComplete();
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
                _logger.LogDebug("Exception thrown in websocket messenger. This is expected when closing.");
            }
            _logger.LogInformation("Websocket closed: {id}", id.ToString());
        }

        private void SetupBroadcastTask()
        {
            _broadcastTask = Task.Run(() => RunBroadcastLoopAsync(_shutdownCts.Token));
        }

        private async Task RunBroadcastLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                var lastNonPingSendUtc = DateTime.UtcNow;

                while (!cancellationToken.IsCancellationRequested)
                {
                    var sentMessage = false;

                    if (_queue.TryTake(out var result, 5000, cancellationToken))
                    {
                        if (!Paused)
                        {
                            await SendMessageToSockets(result, cancellationToken);
                            sentMessage = true;
                            lastNonPingSendUtc = DateTime.UtcNow;
                        }
                    }

                    var shouldPing = !sentMessage && DateTime.UtcNow - lastNonPingSendUtc >= _idlePingInterval;
                    if (!cancellationToken.IsCancellationRequested && shouldPing)
                    {
                        await SendMessageToSockets("ping", cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("BroadcastTask cancelled for shutdown.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BroadcastTask failed unexpectedly.");
            }
        }


        private async Task ReceiveMessage(WebSocket webSocket, CancellationToken cancellationToken)
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var data = await ReadStringAsync(webSocket, cancellationToken);
                if (data == null) continue;
                if(data.Length > 0 && data.Equals("pong"))
                {
                    continue;
                } else if (data.Length >0 && data.StartsWith("TTSComplete: "))
                {
                    await _dispatcher.Publish(new TTSDeleteNotification(data));
                } else if(data.Length > 0 && data.StartsWith("{\"wheel\":"))
                {
                    var wheelSpinComplete = JsonSerializer.Deserialize<WheelSpinComplete>(data);
                    if (wheelSpinComplete != null)
                        await _dispatcher.Publish(new WheelSpinCompleteNotification(wheelSpinComplete));
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

            // Signal shutdown to all operations
            _shutdownCts.Cancel();

            var sockets = await GetSocketSnapshot(_shutdownCts.Token,x => x.WebSocket.State == WebSocketState.Open || x.WebSocket.State == WebSocketState.Connecting);

            var closeTasks = sockets.Select(async socket =>
            {
                try
                {
                    await socket.WebSocket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, String.Empty, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error closing socket {id}", socket.Id);
                }
            });

            await Task.WhenAll(closeTasks);

            try
            {
                if (_broadcastTask != null)
                    await _broadcastTask.WaitAsync(_shutdownTimeout);
                if (_cleanupTask != null)
                    await _cleanupTask.WaitAsync(_shutdownTimeout);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Background tasks did not complete within timeout during shutdown.");
            }

            _logger.LogDebug("Closed all sockets");
        }

        private async Task SendMessageToSockets(string message, CancellationToken cancellationToken)
        {
            var toSentTo = await GetSocketSnapshot(cancellationToken);

            foreach (var websocketConnection in toSentTo)
            {
                if (websocketConnection.WebSocket.State == WebSocketState.Open)
                {
                    if (!websocketConnection.OutboundMessages.Writer.TryWrite(message))
                    {
                        _logger.LogWarning("Outbound websocket queue is full for {id}; dropping message.", websocketConnection.Id);
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
                    var bytes = Encoding.UTF8.GetBytes(message);
                    await connection.WebSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Websocket sender cancelled for {id}.", connection.Id);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error sending websocket message to {id}", connection.Id);
                await RemoveSocketsById([connection.Id]);
            }
        }

        private void SetupCleanUpTask()
        {
            _cleanupTask = Task.Run(() => RunCleanupLoopAsync(_shutdownCts.Token));
        }

        private async Task RunCleanupLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var closedSockets = await PruneClosedSockets(cancellationToken);

                    foreach (var closedWebsocketConnection in closedSockets)
                    {
                        closedWebsocketConnection.SenderCancellation.Cancel();
                        closedWebsocketConnection.OutboundMessages.Writer.TryComplete();
                        _logger.LogInformation("Closing Socket: {id}", closedWebsocketConnection.Id);
                    }

                    if (closedSockets.Count > 0 && !await HasOpenSockets(cancellationToken))
                        _queue.Clear();

                    try
                    {
                        await Task.Delay(_cleanupInterval, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("CleanupTask cancelled for shutdown.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CleanupTask failed unexpectedly.");
            }
            finally
            {
                _logger.LogDebug("CleanupTask exiting.");
            }
        }

        private async Task<List<SocketConnection>> GetSocketSnapshot(CancellationToken cancellationToken, Func<SocketConnection, bool>? predicate = null)
        {
            try
            {
                await _semaphoreSlim.WaitAsync();
                var query = predicate == null ? websocketConnections : websocketConnections.Where(predicate);
                return query.ToList();
            }
            finally { _semaphoreSlim.Release(); }
        }

        private async Task<bool> HasOpenSockets(CancellationToken cancellationToken)
            => (await GetSocketSnapshot(cancellationToken, x => x.WebSocket.State == WebSocketState.Open || x.WebSocket.State == WebSocketState.Connecting)).Count > 0;

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
            }
            finally { _semaphoreSlim.Release(); }

            if (noConnections)
                _queue.Clear();

            foreach (var socket in removedSockets)
            {
                socket.SenderCancellation.Cancel();
                socket.OutboundMessages.Writer.TryComplete();
            }
        }

        private async Task<List<SocketConnection>> PruneClosedSockets(CancellationToken cancellationToken)
        {
            var acquiredSemaphore = false;
            try
            {
                await _semaphoreSlim.WaitAsync(cancellationToken);
                acquiredSemaphore = true;

                var openSockets = websocketConnections.Where(x => x.WebSocket.State == WebSocketState.Open || x.WebSocket.State == WebSocketState.Connecting).ToList();
                var closedSockets = websocketConnections.Where(x => x.WebSocket.State != WebSocketState.Open && x.WebSocket.State != WebSocketState.Connecting).ToList();

                websocketConnections = openSockets;
                return closedSockets;
            }
            finally
            {
                if (acquiredSemaphore)
                    _semaphoreSlim.Release();
            }
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
        public Channel<string> OutboundMessages { get; set; } = null!;
        public Task? SendTask { get; set; }
        public CancellationTokenSource SenderCancellation { get; set; } = null!;
    }
}