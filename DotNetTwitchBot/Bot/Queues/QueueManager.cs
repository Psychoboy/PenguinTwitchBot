using DotNetTwitchBot.Bot.Models.Queues;
using DotNetTwitchBot.Repository;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Queues
{
    public class QueueManager : IQueueManager, IHostedService
    {
        private readonly ConcurrentDictionary<string, IActionQueue> _queues = new();
        private readonly ILogger<QueueManager> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IActionExecutionLogger _executionLogger;
        private CancellationTokenSource? _cancellationTokenSource;

        public const string DefaultQueueName = "default";

        public IActionExecutionLogger ExecutionLogger => _executionLogger;

        public QueueManager(
            ILogger<QueueManager> logger,
            IServiceScopeFactory scopeFactory,
            ILoggerFactory loggerFactory,
            IActionExecutionLogger executionLogger)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _loggerFactory = loggerFactory;
            _executionLogger = executionLogger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Create default queue
            var defaultQueue = new ActionQueue(
                DefaultQueueName,
                isBlocking: true,
                maxConcurrentActions: 1,
                _loggerFactory.CreateLogger<ActionQueue>(),
                _scopeFactory,
                _executionLogger);

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
                _executionLogger)
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

            // Update the running queue
            if (_queues.TryGetValue(config.Name, out var queue))
            {
                queue.IsEnabled = config.Enabled;
                _logger.LogInformation("Updated queue {QueueName}", config.Name);
            }

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

            return statistics;
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
                        _executionLogger)
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
