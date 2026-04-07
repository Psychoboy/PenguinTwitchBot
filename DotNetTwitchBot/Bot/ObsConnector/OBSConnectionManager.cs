using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Obs;
using Microsoft.EntityFrameworkCore;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;

namespace DotNetTwitchBot.Bot.ObsConnector
{
    /// <summary>
    /// Manages multiple OBS WebSocket connections
    /// </summary>
    public interface IOBSConnectionManager
    {
        Task<List<OBSConnection>> GetAllConnectionsAsync();
        Task<OBSConnection?> GetConnectionAsync(int id);
        Task<OBSConnection> CreateConnectionAsync(OBSConnection connection);
        Task<OBSConnection> UpdateConnectionAsync(OBSConnection connection);
        Task DeleteConnectionAsync(int id);
        Task<bool> TestConnectionAsync(string url, string password);
        ManagedOBSConnection? GetManagedConnection(int id);
        ManagedOBSConnection? GetManagedConnection(string name);
        List<ManagedOBSConnection> GetAllManagedConnections();
        Task ReloadConnectionsAsync();
    }

    public class OBSConnectionManager : IOBSConnectionManager, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OBSConnectionManager> _logger;
        private readonly Dictionary<int, ManagedOBSConnection> _connections = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _isDisposed;

        public OBSConnectionManager(
            IServiceScopeFactory scopeFactory,
            ILogger<OBSConnectionManager> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing OBS Connection Manager");
            await ReloadConnectionsAsync();
        }

        public async Task<List<OBSConnection>> GetAllConnectionsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await dbContext.OBSConnections.OrderBy(c => c.Name).ToListAsync();
        }

        public async Task<OBSConnection?> GetConnectionAsync(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await dbContext.OBSConnections.FindAsync(id);
        }

        public async Task<OBSConnection> CreateConnectionAsync(OBSConnection connection)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            connection.CreatedAt = DateTime.UtcNow;
            connection.UpdatedAt = DateTime.UtcNow;

            dbContext.OBSConnections.Add(connection);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Created OBS connection '{Name}' (ID: {Id})", connection.Name, connection.Id);

            // Start the connection if enabled
            if (connection.Enabled)
            {
                await StartConnectionAsync(connection);
            }

            return connection;
        }

        public async Task<OBSConnection> UpdateConnectionAsync(OBSConnection connection)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var existing = await dbContext.OBSConnections.FindAsync(connection.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"OBS connection with ID {connection.Id} not found");
            }

            // Update fields
            existing.Name = connection.Name;
            existing.Url = connection.Url;
            existing.Password = connection.Password;
            existing.Enabled = connection.Enabled;
            existing.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated OBS connection '{Name}' (ID: {Id})", connection.Name, connection.Id);

            // Restart connection to apply changes
            await RestartConnectionAsync(existing);

            return existing;
        }

        public async Task DeleteConnectionAsync(int id)
        {
            // Stop and remove managed connection
            await _lock.WaitAsync();
            try
            {
                if (_connections.TryGetValue(id, out var managedConnection))
                {
                    await managedConnection.StopAsync();
                    managedConnection.Dispose();
                    _connections.Remove(id);
                }
            }
            finally
            {
                _lock.Release();
            }

            // Delete from database
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var connection = await dbContext.OBSConnections.FindAsync(id);
            if (connection != null)
            {
                dbContext.OBSConnections.Remove(connection);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Deleted OBS connection '{Name}' (ID: {Id})", connection.Name, connection.Id);
            }
        }

        public async Task<bool> TestConnectionAsync(string url, string password)
        {
            try
            {
                var testObs = new OBSWebsocket();
                var tcs = new TaskCompletionSource<bool>();

                EventHandler? connectedHandler = null;
                EventHandler<ObsDisconnectionInfo>? disconnectedHandler = null;

                connectedHandler = (sender, e) =>
                {
                    testObs.Connected -= connectedHandler;
                    testObs.Disconnected -= disconnectedHandler;
                    try
                    {
                        testObs.Disconnect();
                    }
                    catch { }
                    tcs.TrySetResult(true);
                };

                disconnectedHandler = (sender, e) =>
                {
                    testObs.Connected -= connectedHandler;
                    testObs.Disconnected -= disconnectedHandler;
                    tcs.TrySetResult(false);
                };

                testObs.Connected += connectedHandler;
                testObs.Disconnected += disconnectedHandler;

                testObs.ConnectAsync(url, password);

                // Wait for connection or timeout after 5 seconds
                var timeoutTask = Task.Delay(5000);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    testObs.Connected -= connectedHandler;
                    testObs.Disconnected -= disconnectedHandler;
                    try
                    {
                        testObs.Disconnect();
                    }
                    catch { }
                    return false;
                }

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test OBS connection to {Url}", url);
                return false;
            }
        }

        public ManagedOBSConnection? GetManagedConnection(int id)
        {
            _lock.Wait();
            try
            {
                _connections.TryGetValue(id, out var connection);
                return connection;
            }
            finally
            {
                _lock.Release();
            }
        }

        public ManagedOBSConnection? GetManagedConnection(string name)
        {
            _lock.Wait();
            try
            {
                return _connections.Values.FirstOrDefault(c => 
                    c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _lock.Release();
            }
        }

        public List<ManagedOBSConnection> GetAllManagedConnections()
        {
            _lock.Wait();
            try
            {
                return _connections.Values.ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ReloadConnectionsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                // Stop all existing connections
                foreach (var connection in _connections.Values)
                {
                    await connection.StopAsync();
                    connection.Dispose();
                }
                _connections.Clear();

                // Load connections from database
                var connections = await GetAllConnectionsAsync();

                // Start enabled connections (within the lock)
                var enabledConnections = connections.Where(c => c.Enabled).ToList();
                foreach (var config in enabledConnections)
                {
                    await StartConnectionCoreAsync(config);
                }

                _logger.LogInformation("Reloaded {Count} OBS connection(s), {EnabledCount} enabled", 
                    connections.Count, enabledConnections.Count);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task StartConnectionAsync(OBSConnection config)
        {
            await _lock.WaitAsync();
            try
            {
                await StartConnectionCoreAsync(config);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task StartConnectionCoreAsync(OBSConnection config)
        {
            // This method assumes the caller already holds the lock
            // Remove existing if present
            if (_connections.TryGetValue(config.Id, out var existing))
            {
                await existing.StopAsync();
                existing.Dispose();
                _connections.Remove(config.Id);
            }

            // Create new managed connection
            using var scope = _scopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ManagedOBSConnection>>();
            var obsWebsocket = new OBSWebsocket();

            var managedConnection = new ManagedOBSConnection(config, obsWebsocket, logger);

            // Wire up events to update database
            managedConnection.Connected += OnConnectionConnected;
            managedConnection.Disconnected += OnConnectionDisconnected;

            _connections[config.Id] = managedConnection;

            await managedConnection.StartAsync();
        }

        private async Task RestartConnectionAsync(OBSConnection config)
        {
            await _lock.WaitAsync();
            try
            {
                // Stop existing connection
                if (_connections.TryGetValue(config.Id, out var existing))
                {
                    await existing.StopAsync();
                    existing.Dispose();
                    _connections.Remove(config.Id);
                }

                // Start if enabled
                if (config.Enabled)
                {
                    await StartConnectionCoreAsync(config);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private async void OnConnectionConnected(object? sender, OBSConnectionEventArgs e)
        {
            if (_isDisposed)
                return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var connection = await dbContext.OBSConnections.FindAsync(e.ConnectionId);
                if (connection != null)
                {
                    connection.IsConnected = true;
                    connection.LastConnected = DateTime.UtcNow;
                    connection.LastError = null;
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating connection status for '{Name}'", e.ConnectionName);
            }
        }

        private async void OnConnectionDisconnected(object? sender, OBSConnectionEventArgs e)
        {
            if (_isDisposed)
                return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var connection = await dbContext.OBSConnections.FindAsync(e.ConnectionId);
                if (connection != null)
                {
                    connection.IsConnected = false;
                    connection.LastDisconnected = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating connection status for '{Name}'", e.ConnectionName);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _lock.Wait();
            try
            {
                foreach (var connection in _connections.Values)
                {
                    try
                    {
                        // Unsubscribe from events before stopping to prevent events during disposal
                        connection.Connected -= OnConnectionConnected;
                        connection.Disconnected -= OnConnectionDisconnected;

                        connection.StopAsync().GetAwaiter().GetResult();
                        connection.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing OBS connection '{Name}'", connection.Name);
                    }
                }

                _connections.Clear();
            }
            finally
            {
                _lock.Release();
            }

            _lock.Dispose();
        }
    }
}
