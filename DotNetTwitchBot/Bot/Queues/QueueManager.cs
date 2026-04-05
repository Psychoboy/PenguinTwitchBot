using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Bot.Models.Queues;
using DotNetTwitchBot.Repository;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Queues
{
    public class QueueManager : IQueueManager, IHostedService
    {
        private readonly ConcurrentDictionary<string, IActionQueue> _queues = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<QueueManager> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IActionExecutionLogger _executionLogger;
        private readonly IHubContext<MainHub> _hubContext;
        private CancellationTokenSource? _cancellationTokenSource;

        public const string DefaultQueueName = "Default";

        public IActionExecutionLogger ExecutionLogger => _executionLogger;

        public QueueManager(
            ILogger<QueueManager> logger,
            IServiceScopeFactory scopeFactory,
            ILoggerFactory loggerFactory,
            IActionExecutionLogger executionLogger,
            IHubContext<MainHub> hubContext)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _loggerFactory = loggerFactory;
            _executionLogger = executionLogger;
            _hubContext = hubContext;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Create default queue
            var defaultQueue = new ActionQueue(
                DefaultQueueName,
                isBlocking: false,
                maxConcurrentActions: 50,
                _loggerFactory.CreateLogger<ActionQueue>(),
                _scopeFactory,
                _executionLogger,
                _hubContext);

            _queues.TryAdd(DefaultQueueName, defaultQueue);
            await defaultQueue.StartAsync(_cancellationTokenSource.Token);

            // Load queues from database
            await LoadQueuesFromDatabaseAsync(_cancellationTokenSource.Token);

            _logger.LogInformation("QueueManager started with {QueueCount} queues", _queues.Count);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource?.Cancel();
            await StopAllQueuesAsync();
            _logger.LogInformation("QueueManager stopped");
        }

        public async Task<IActionQueue> GetQueueAsync(string queueName)
        {
            if (_queues.TryGetValue(queueName, out var queue))
            {
                return queue;
            }

            // If queue doesn't exist, return default queue
            _logger.LogWarning("Queue {QueueName} not found, using default queue", queueName);
            return _queues[DefaultQueueName];
        }

        public async Task<QueueConfiguration> CreateQueueAsync(QueueConfiguration config)
        {
            if (config.Name.Equals(DefaultQueueName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot create a queue with the reserved name 'default'");
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var existing = await db.QueueConfigurations.Find(q => q.Name == config.Name).FirstOrDefaultAsync();
            if (existing != null)
            {
                throw new InvalidOperationException($"Queue with name '{config.Name}' already exists");
            }

            config.CreatedAt = DateTime.UtcNow;
            config.UpdatedAt = DateTime.UtcNow;

            await db.QueueConfigurations.AddAsync(config);
            await db.SaveChangesAsync();

            // Create and start the queue
            var queue = new ActionQueue(
                config.Name,
                config.IsBlocking,
                config.MaxConcurrentActions,
                _loggerFactory.CreateLogger<ActionQueue>(),
                _scopeFactory,
                _executionLogger,
                _hubContext)
            {
                IsEnabled = config.Enabled
            };

            if (_queues.TryAdd(config.Name, queue))
            {
                if (_cancellationTokenSource != null)
                {
                    await queue.StartAsync(_cancellationTokenSource.Token);
                }
                _logger.LogInformation("Created and started queue {QueueName}", config.Name);
            }

            return config;
        }

        public async Task<QueueConfiguration> UpdateQueueAsync(QueueConfiguration config)
        {
            if (config.Name.Equals(DefaultQueueName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot update the default queue");
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var existing = await db.QueueConfigurations.Find(q => q.Name == config.Name).FirstOrDefaultAsync();
            if (existing == null)
            {
                throw new InvalidOperationException($"Queue with name '{config.Name}' not found");
            }

            existing.IsBlocking = config.IsBlocking;
            existing.Enabled = config.Enabled;
            existing.MaxConcurrentActions = config.MaxConcurrentActions;
            existing.UpdatedAt = DateTime.UtcNow;

            db.QueueConfigurations.Update(existing);
            await db.SaveChangesAsync();

            // Check if QueueManager is running
            if (_cancellationTokenSource == null || _cancellationTokenSource.Token.IsCancellationRequested)
            {
                _logger.LogWarning("Cannot update queue {QueueName} - QueueManager is not running", existing.Name);
                throw new InvalidOperationException("Queue Manager is not running. Please restart the application.");
            }

            // Create new queue first before removing old one to avoid losing it if creation fails
            var newQueue = new ActionQueue(
                existing.Name,
                existing.IsBlocking,
                existing.MaxConcurrentActions,
                _loggerFactory.CreateLogger<ActionQueue>(),
                _scopeFactory,
                _executionLogger,
                _hubContext)
            {
                IsEnabled = existing.Enabled
            };

            // Start the new queue with proper error handling
            try
            {
                await newQueue.StartAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Queue start was cancelled for {QueueName}", existing.Name);
                throw new InvalidOperationException("Queue update was cancelled. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start new queue {QueueName}", existing.Name);
                throw new InvalidOperationException($"Failed to start updated queue: {ex.Message}");
            }

            // Now remove and stop the old queue
            IActionQueue? oldQueue = null;
            if (_queues.TryRemove(existing.Name, out oldQueue))
            {
                _logger.LogInformation("Removed old queue {QueueName} for update", existing.Name);
            }

            // Add the new queue
            if (!_queues.TryAdd(existing.Name, newQueue))
            {
                // This shouldn't happen since we just removed it, but handle it just in case
                _logger.LogError("Failed to add updated queue {QueueName} to dictionary", existing.Name);

                // Stop the new queue and try to restore old queue
                await newQueue.StopAsync();

                if (oldQueue != null && _queues.TryAdd(existing.Name, oldQueue))
                {
                    _logger.LogWarning("Restored old queue {QueueName} after failed update", existing.Name);
                    throw new InvalidOperationException($"Failed to update queue '{existing.Name}' - changes reverted");
                }

                throw new InvalidOperationException($"Failed to update queue '{existing.Name}'");
            }

            // Stop old queue after new one is added successfully
            if (oldQueue != null)
            {
                try
                {
                    await oldQueue.StopAsync();
                    _logger.LogInformation("Stopped old queue {QueueName} after successful update", existing.Name);
                }
                catch (OperationCanceledException)
                {
                    // This is expected - the old queue was cancelled, which is normal during shutdown
                    _logger.LogDebug("Old queue {QueueName} was already cancelled during stop", existing.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unexpected error stopping old queue {QueueName}, but update completed successfully", existing.Name);
                }
            }

            _logger.LogInformation("Successfully updated queue {QueueName}", existing.Name);
            return existing;
        }

        public async Task DeleteQueueAsync(string queueName)
        {
            if (queueName.Equals(DefaultQueueName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot delete the default queue");
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var existing = await db.QueueConfigurations.Find(q => q.Name == queueName).FirstOrDefaultAsync();
            if (existing != null)
            {
                db.QueueConfigurations.Remove(existing);
                await db.SaveChangesAsync();
            }

            if (_queues.TryRemove(queueName, out var queue))
            {
                await queue.StopAsync();
                _logger.LogInformation("Deleted and stopped queue {QueueName}", queueName);
            }
        }

        public async Task<List<QueueConfiguration>> GetAllQueuesAsync()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var configs = await db.QueueConfigurations.GetAllAsync();
            return configs.ToList();
        }

        public async Task<QueueStatistics> GetQueueStatisticsAsync(string queueName)
        {
            var queue = await GetQueueAsync(queueName);
            
            return new QueueStatistics
            {
                QueueName = queue.Name,
                PendingActions = queue.PendingCount,
                CompletedActions = queue.CompletedCount,
                IsBlocking = queue.IsBlocking,
                IsEnabled = queue.IsEnabled,
                MaxConcurrentActions = queue.MaxConcurrentActions,
                CurrentlyExecuting = queue.CurrentlyExecuting
            };
        }

        public async Task<List<QueueStatistics>> GetAllQueueStatisticsAsync()
        {
            var statistics = new List<QueueStatistics>();

            foreach (var queue in _queues.Values)
            {
                statistics.Add(new QueueStatistics
                {
                    QueueName = queue.Name,
                    PendingActions = queue.PendingCount,
                    CompletedActions = queue.CompletedCount,
                    IsBlocking = queue.IsBlocking,
                    IsEnabled = queue.IsEnabled,
                    MaxConcurrentActions = queue.MaxConcurrentActions,
                    CurrentlyExecuting = queue.CurrentlyExecuting
                });
            }

            // Sort with Default queue first, then alphabetically
            return statistics
                .OrderBy(s => s.QueueName != DefaultQueueName)
                .ThenBy(s => s.QueueName)
                .ToList();
        }

        public async Task StartAllQueuesAsync(CancellationToken cancellationToken)
        {
            var tasks = _queues.Values.Select(q => q.StartAsync(cancellationToken));
            await Task.WhenAll(tasks);
        }

        public async Task StopAllQueuesAsync()
        {
            var tasks = _queues.Values.Select(q => q.StopAsync());
            await Task.WhenAll(tasks);
        }

        private async Task LoadQueuesFromDatabaseAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var configs = await db.QueueConfigurations.GetAllAsync();

            foreach (var config in configs)
            {
                try
                {
                    var queue = new ActionQueue(
                        config.Name,
                        config.IsBlocking,
                        config.MaxConcurrentActions,
                        _loggerFactory.CreateLogger<ActionQueue>(),
                        _scopeFactory,
                        _executionLogger,
                        _hubContext)
                    {
                        IsEnabled = config.Enabled
                    };

                    if (_queues.TryAdd(config.Name, queue))
                    {
                        await queue.StartAsync(cancellationToken);
                        _logger.LogInformation("Loaded and started queue {QueueName} from database", config.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading queue {QueueName}", config.Name);
                }
            }
        }
    }
}
