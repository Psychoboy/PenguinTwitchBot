using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DotNetTwitchBot.Application.TTS;
using DotNetTwitchBot.Application.WheelSpinNotifications;
using DotNetTwitchBot.Extensions;

namespace DotNetTwitchBot.Bot.Notifications
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
            _broadcastTask = Task.Run(async () =>
            {
                try
                {
                    var idlePingInterval = TimeSpan.FromSeconds(5);
                    var lastNonPingSendUtc = DateTime.UtcNow;

                    while (!_shutdownCts.Token.IsCancellationRequested)
                    {
                        var sentMessage = false;

                        if (_queue.TryTake(out var result, 5000, _shutdownCts.Token))
                        {
                            if (!Paused)
                            {
                                await SendMessageToSockets(result);
                                sentMessage = true;
                                lastNonPingSendUtc = DateTime.UtcNow;
                            }
                        }

                        var shouldPing = !sentMessage && DateTime.UtcNow - lastNonPingSendUtc >= idlePingInterval;
                        if (!_shutdownCts.Token.IsCancellationRequested && shouldPing)
                        {
                            await SendMessageToSockets("ping");
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
            });
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
                    _logger.LogDebug(ex, "Error closing socket {id}", socket.Id);
                }
            });

            await Task.WhenAll(closeTasks);

            var shutdownTimeout = TimeSpan.FromSeconds(5);
            try
            {
                if (_broadcastTask != null)
                    await _broadcastTask.WaitAsync(shutdownTimeout);
                if (_cleanupTask != null)
                    await _cleanupTask.WaitAsync(shutdownTimeout);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Background tasks did not complete within timeout during shutdown.");
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

            var failedSocketIds = new ConcurrentBag<Guid>();
            var tasks = toSentTo.Select(async websocketConnection =>
            {
                if (websocketConnection.WebSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        var bytes = Encoding.UTF8.GetBytes(message);
                        var arraySegment = new ArraySegment<byte>(bytes);
                        await websocketConnection.WebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        failedSocketIds.Add(websocketConnection.Id);
                        _logger.LogDebug(ex, "Error sending websocket message to {id}", websocketConnection.Id);
                    }
                }
            });
            await Task.WhenAll(tasks);

            if (!failedSocketIds.IsEmpty)
            {
                var failedIds = failedSocketIds.ToHashSet();
                try
                {
                    await _semaphoreSlim.WaitAsync();
                    websocketConnections = websocketConnections.Where(x => !failedIds.Contains(x.Id)).ToList();
                }
                finally { _semaphoreSlim.Release(); }
            }
        }

        private void SetupCleanUpTask()
        {
            _cleanupTask = Task.Run(async () =>
            {
                try
                {
                    while (!_shutdownCts.Token.IsCancellationRequested)
                    {
                        IEnumerable<SocketConnection> openSockets;
                        IEnumerable<SocketConnection> closedSockets;
                        bool acquiredSemaphore = false;

                        try
                        {
                            await _semaphoreSlim.WaitAsync(_shutdownCts.Token);
                            acquiredSemaphore = true;
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }

                        try
                        {
                            openSockets = websocketConnections.Where(x => x.WebSocket.State == WebSocketState.Open || x.WebSocket.State == WebSocketState.Connecting);
                            closedSockets = websocketConnections.Where(x => x.WebSocket.State != WebSocketState.Open && x.WebSocket.State != WebSocketState.Connecting);

                            websocketConnections = openSockets.ToList();
                        }
                        finally
                        {
                            if (acquiredSemaphore)
                                _semaphoreSlim.Release();
                        }

                        foreach (var closedWebsocketConnection in closedSockets)
                        {
                            _logger.LogInformation("Closing Socket: {id}", closedWebsocketConnection.Id);
                        }

                        try
                        {
                            await Task.Delay(5000, _shutdownCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CleanupTask failed unexpectedly.");
                }
                finally
                {
                    _logger.LogDebug("CleanupTask exiting.");
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