using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types.Events;
using DotNetTwitchBot.Bot.Models.Obs;

namespace DotNetTwitchBot.Bot.ObsConnector
{
    /// <summary>
    /// Manages a single OBS WebSocket connection with automatic reconnection
    /// </summary>
    public class ManagedOBSConnection : IDisposable
    {
        private readonly OBSConnection _config;
        private readonly IOBSWebsocket _obs;
        private readonly ILogger<ManagedOBSConnection> _logger;
        private readonly Timer _reconnectTimer;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private bool _isDisposed;
        private bool _shouldBeConnected;
        private int _reconnectAttempts;
        private const int MaxReconnectDelay = 30000; // 30 seconds max
        private const int InitialReconnectDelay = 1000; // 1 second initial
        private static readonly int MaxReconnectAttemptsBeforeCap = 
            (int)Math.Floor(Math.Log2((double)MaxReconnectDelay / InitialReconnectDelay));
        
        public int Id => _config.Id;
        public string Name => _config.Name;
        public bool IsConnected { get; private set; }
        public DateTime? LastConnected { get; private set; }
        public DateTime? LastDisconnected { get; private set; }
        public string? LastError { get; private set; }

        public event EventHandler<OBSConnectionEventArgs>? Connected;
        public event EventHandler<OBSConnectionEventArgs>? Disconnected;
        public event EventHandler<OBSSceneChangedEventArgs>? SceneChanged;

        public ManagedOBSConnection(
            OBSConnection config,
            IOBSWebsocket obsWebsocket,
            ILogger<ManagedOBSConnection> logger)
        {
            _config = config;
            _obs = obsWebsocket;
            _logger = logger;
            _reconnectTimer = new Timer(ReconnectTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Wire up OBS events
            _obs.Connected += OnConnected;
            _obs.Disconnected += OnDisconnected;
            _obs.CurrentProgramSceneChanged += OnSceneChanged;
        }

        public async Task StartAsync()
        {
            if (!_config.Enabled)
            {
                _logger.LogInformation("OBS connection '{Name}' is disabled", _config.Name);
                return;
            }

            _shouldBeConnected = true;
            await ConnectAsync();
        }

        public async Task StopAsync()
        {
            _shouldBeConnected = false;
            _reconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            await _connectionLock.WaitAsync();
            try
            {
                if (IsConnected)
                {
                    _obs.Disconnect();
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task ConnectAsync()
        {
            if (_isDisposed || !_shouldBeConnected)
                return;

            await _connectionLock.WaitAsync();
            try
            {
                if (IsConnected)
                    return;

                _logger.LogDebug("Connecting to OBS '{Name}' at {Url}", _config.Name, _config.Url);
                _obs.ConnectAsync(_config.Url, _config.Password);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                _logger.LogError(ex, "Failed to connect to OBS '{Name}'", _config.Name);
                ScheduleReconnect();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void ScheduleReconnect()
        {
            if (!_shouldBeConnected || _isDisposed)
                return;

            // Exponential backoff with max delay
            int delay;
            if (_reconnectAttempts > MaxReconnectAttemptsBeforeCap)
            {
                delay = MaxReconnectDelay;
            }
            else
            {
                delay = InitialReconnectDelay * (int)Math.Pow(2, _reconnectAttempts);
            }

            _reconnectAttempts++;
            _logger.LogDebug(
                "Scheduling reconnect to OBS '{Name}' in {Delay}ms (attempt {Attempt})",
                _config.Name, delay, _reconnectAttempts);

            _reconnectTimer.Change(delay, Timeout.Infinite);
        }

        private async void ReconnectTimerCallback(object? state)
        {
            await ConnectAsync();
        }

        private void OnConnected(object? sender, EventArgs e)
        {
            IsConnected = true;
            LastConnected = DateTime.UtcNow;
            LastError = null;
            _reconnectAttempts = 0;

            _logger.LogInformation("Successfully connected to OBS '{Name}'", _config.Name);
            Connected?.Invoke(this, new OBSConnectionEventArgs(_config.Id, _config.Name));
        }

        private void OnDisconnected(object? sender, ObsDisconnectionInfo e)
        {
            IsConnected = false;
            LastDisconnected = DateTime.UtcNow;

            var reason = e.DisconnectReason ?? "Unknown";
            _logger.LogDebug("Disconnected from OBS '{Name}': {Reason}", _config.Name, reason);

            Disconnected?.Invoke(this, new OBSConnectionEventArgs(_config.Id, _config.Name));

            if (_shouldBeConnected)
            {
                ScheduleReconnect();
            }
        }

        private void OnSceneChanged(object? sender, ProgramSceneChangedEventArgs e)
        {
            SceneChanged?.Invoke(this, new OBSSceneChangedEventArgs(
                _config.Id,
                _config.Name,
                e.SceneName));
        }

        // Public methods for OBS operations
        public async Task<bool> ExecuteAsync(Func<IOBSWebsocket, Task> operation)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot execute operation on OBS '{Name}' - not connected", _config.Name);
                return false;
            }

            try
            {
                await operation(_obs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing operation on OBS '{Name}'", _config.Name);
                return false;
            }
        }

        public bool Execute(Action<IOBSWebsocket> operation)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot execute operation on OBS '{Name}' - not connected", _config.Name);
                return false;
            }

            try
            {
                operation(_obs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing operation on OBS '{Name}'", _config.Name);
                return false;
            }
        }

        public T? Execute<T>(Func<IOBSWebsocket, T> operation)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot execute operation on OBS '{Name}' - not connected", _config.Name);
                return default;
            }

            try
            {
                return operation(_obs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing operation on OBS '{Name}'", _config.Name);
                return default;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _shouldBeConnected = false;

            _reconnectTimer.Dispose();
            _connectionLock.Dispose();

            if (_obs != null)
            {
                try
                {
                    _obs.Connected -= OnConnected;
                    _obs.Disconnected -= OnDisconnected;
                    _obs.CurrentProgramSceneChanged -= OnSceneChanged;

                    if (IsConnected)
                    {
                        _obs.Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing OBS connection '{Name}'", _config.Name);
                }
            }
        }
    }

    public class OBSConnectionEventArgs : EventArgs
    {
        public int ConnectionId { get; }
        public string ConnectionName { get; }

        public OBSConnectionEventArgs(int connectionId, string connectionName)
        {
            ConnectionId = connectionId;
            ConnectionName = connectionName;
        }
    }

    public class OBSSceneChangedEventArgs : OBSConnectionEventArgs
    {
        public string SceneName { get; }

        public OBSSceneChangedEventArgs(int connectionId, string connectionName, string sceneName)
            : base(connectionId, connectionName)
        {
            SceneName = sceneName;
        }
    }
}
