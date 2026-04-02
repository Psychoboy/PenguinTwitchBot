using DotNetTwitchBot.Bot.Models.Actions;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace DotNetTwitchBot.Bot.Queues
{
    public class ActionQueue : IActionQueue
    {
        private readonly Channel<QueuedAction> _channel;
        private readonly ILogger<ActionQueue> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IActionExecutionLogger _executionLogger;
        private readonly SemaphoreSlim _semaphore;
        private long _completedCount;
        private int _currentlyExecuting;
        private int _pendingCount;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _processingTask;

        public string Name { get; }
        public bool IsBlocking { get; }
        public bool IsEnabled { get; set; }
        public int MaxConcurrentActions { get; }
        public int PendingCount => _pendingCount;
        public long CompletedCount => Interlocked.Read(ref _completedCount);
        public int CurrentlyExecuting => _currentlyExecuting;

        public ActionQueue(
            string name,
            bool isBlocking,
            int maxConcurrentActions,
            ILogger<ActionQueue> logger,
            IServiceScopeFactory scopeFactory,
            IActionExecutionLogger executionLogger)
        {
            Name = name;
            IsBlocking = isBlocking;
            MaxConcurrentActions = maxConcurrentActions;
            IsEnabled = true;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _executionLogger = executionLogger;

            var channelOptions = new UnboundedChannelOptions
            {
                SingleReader = isBlocking,
                SingleWriter = false
            };
            _channel = Channel.CreateUnbounded<QueuedAction>(channelOptions);
            _semaphore = new SemaphoreSlim(isBlocking ? 1 : maxConcurrentActions);
        }

        public async Task EnqueueAsync(ActionType action, Dictionary<string, string> variables)
        {
            if (!IsEnabled)
            {
                _logger.LogWarning("Queue {QueueName} is disabled, action {ActionName} not enqueued", Name, action.Name);
                return;
            }

            var logId = _executionLogger.LogActionEnqueued(action.Name, Name, variables);
            var queuedAction = new QueuedAction(action, variables, logId);
            await _channel.Writer.WriteAsync(queuedAction);
            Interlocked.Increment(ref _pendingCount);
            _logger.LogDebug("Action {ActionName} enqueued to {QueueName}", action.Name, Name);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            if (IsBlocking)
            {
                _processingTask = ProcessBlockingQueueAsync(_cancellationTokenSource.Token);
            }
            else
            {
                _processingTask = ProcessNonBlockingQueueAsync(_cancellationTokenSource.Token);
            }

            _logger.LogInformation("Queue {QueueName} started (Blocking: {IsBlocking}, MaxConcurrent: {MaxConcurrent})", 
                Name, IsBlocking, MaxConcurrentActions);
            
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            _cancellationTokenSource?.Cancel();
            _channel.Writer.Complete();

            if (_processingTask != null)
            {
                await _processingTask;
            }

            _logger.LogInformation("Queue {QueueName} stopped", Name);
        }

        private async Task ProcessBlockingQueueAsync(CancellationToken cancellationToken)
        {
            await foreach (var queuedAction in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!IsEnabled)
                {
                    _logger.LogDebug("Queue {QueueName} is disabled, skipping action", Name);
                    continue;
                }

                try
                {
                    await ExecuteActionAsync(queuedAction, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing action {ActionName} in queue {QueueName}", 
                        queuedAction.Action.Name, Name);
                }
            }
        }

        private async Task ProcessNonBlockingQueueAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            await foreach (var queuedAction in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!IsEnabled)
                {
                    _logger.LogDebug("Queue {QueueName} is disabled, skipping action", Name);
                    continue;
                }

                await _semaphore.WaitAsync(cancellationToken);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await ExecuteActionAsync(queuedAction, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing action {ActionName} in queue {QueueName}", 
                            queuedAction.Action.Name, Name);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);

                // Clean up completed tasks
                tasks.RemoveAll(t => t.IsCompleted);
            }

            // Wait for all remaining tasks to complete
            await Task.WhenAll(tasks);
        }

        private async Task ExecuteActionAsync(QueuedAction queuedAction, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _currentlyExecuting);
            Interlocked.Decrement(ref _pendingCount);

            _executionLogger.UpdateActionStarted(queuedAction.LogId);

            try
            {
                _logger.LogDebug("Executing action {ActionName} in queue {QueueName}", 
                    queuedAction.Action.Name, Name);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var actionService = scope.ServiceProvider.GetRequiredService<Actions.Action>();

                await actionService.RunAction(queuedAction.Variables, queuedAction.Action);

                Interlocked.Increment(ref _completedCount);

                _executionLogger.UpdateActionCompleted(queuedAction.LogId, queuedAction.Variables);

                _logger.LogDebug("Completed action {ActionName} in queue {QueueName}", 
                    queuedAction.Action.Name, Name);
            }
            catch (Exception ex)
            {
                _executionLogger.UpdateActionFailed(queuedAction.LogId, ex.Message);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _currentlyExecuting);
            }
        }

        private record QueuedAction(ActionType Action, Dictionary<string, string> Variables, Guid LogId);
    }
}
