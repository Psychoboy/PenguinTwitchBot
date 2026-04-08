using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Bot.Models.Queues;
using DotNetTwitchBot.Bot.WebSocketEvents;
using Microsoft.AspNetCore.SignalR;
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
        private readonly IHubContext<MainHub>? _hubContext;
        private readonly IWsEventHandler _wsEventHandler;
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
            IActionExecutionLogger executionLogger,
            IWsEventHandler wsEventHandler,
            IHubContext<MainHub>? hubContext = null)
        {
            Name = name;
            IsBlocking = isBlocking;
            MaxConcurrentActions = maxConcurrentActions;
            IsEnabled = true;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _executionLogger = executionLogger;
            _hubContext = hubContext;
            _wsEventHandler = wsEventHandler;

            var channelOptions = new UnboundedChannelOptions
            {
                SingleReader = isBlocking,
                SingleWriter = false
            };
            _channel = Channel.CreateUnbounded<QueuedAction>(channelOptions);
            _semaphore = new SemaphoreSlim(isBlocking ? 1 : maxConcurrentActions);
        }

        public async Task EnqueueAsync(ActionType action, ConcurrentDictionary<string, string> variables, Guid? parentLogId = null, int? parentSubActionIndex = null)
        {
            if (!IsEnabled)
            {
                _logger.LogWarning("Queue {QueueName} is disabled, action {ActionName} not enqueued", Name, action.Name);
                return;
            }

            var logId = _executionLogger.LogActionEnqueued(action.Name, Name, variables);
            var queuedAction = new QueuedAction(action, variables, logId, parentLogId, parentSubActionIndex);
            await _channel.Writer.WriteAsync(queuedAction);
            Interlocked.Increment(ref _pendingCount);
            _logger.LogDebug("Action {ActionName} enqueued to {QueueName}", action.Name, Name);

            await SendEventToWs(queuedAction);

            // Notify clients about queue statistics change
            await SendQueueStatsUpdateAsync();
        }

        private async Task SendEventToWs(QueuedAction queuedAction)
        {
            var wsEvent = new WsEvent
            {
                TimeStamp = DateTime.UtcNow,
                Event = new WsEventType { Source = "Raw", Type = "Action" },
                Data = new Dictionary<string, object>
                {
                    { "queueName", Name },
                    { "name", queuedAction.Action.Name },
                    { "variables", queuedAction.Variables },
                    { "logId", queuedAction.LogId }
                }
            };
            await _wsEventHandler.AddToQueue(wsEvent);
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
            try
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
            catch (OperationCanceledException)
            {
                // Expected when the queue is stopping - log as debug only
                _logger.LogDebug("Queue {QueueName} processing cancelled during shutdown", Name);
            }
        }

        private async Task ProcessNonBlockingQueueAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            try
            {
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
                    });

                    tasks.Add(task);

                    // Clean up completed tasks
                    tasks.RemoveAll(t => t.IsCompleted);
                }

                // Wait for all remaining tasks to complete
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Expected when the queue is stopping - log as debug only
                _logger.LogDebug("Queue {QueueName} processing cancelled during shutdown", Name);

                // Wait for any in-flight tasks to complete
                if (tasks.Any())
                {
                    try
                    {
                        await Task.WhenAll(tasks);
                    } catch (OperationCanceledException)
                    {
                        _logger.LogDebug("Some tasks in queue {QueueName} were cancelled during shutdown", Name);
                    }
                }
            }
        }

        private async Task ExecuteActionAsync(QueuedAction queuedAction, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _currentlyExecuting);
            Interlocked.Decrement(ref _pendingCount);

            _executionLogger.UpdateActionStarted(queuedAction.LogId);

            // Link parent and child if this is a nested action
            if (queuedAction.ParentLogId.HasValue && queuedAction.ParentSubActionIndex.HasValue)
            {
                _executionLogger.LinkChildAction(queuedAction.ParentLogId.Value, queuedAction.ParentSubActionIndex.Value, queuedAction.LogId);
                _executionLogger.SetParentAction(queuedAction.LogId, queuedAction.ParentLogId.Value);
            }

            // Notify clients about queue statistics change
            await SendQueueStatsUpdateAsync();

            try
            {
                _logger.LogDebug("Executing action {ActionName} in queue {QueueName}", 
                    queuedAction.Action.Name, Name);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var actionService = scope.ServiceProvider.GetRequiredService<Actions.IAction>();

                // Set up the SubAction execution context for this action
                var contextLogger = scope.ServiceProvider.GetRequiredService<ILogger<SubActionExecutionContext>>();
                var executionContext = new SubActionExecutionContext(queuedAction.LogId, _executionLogger, contextLogger);

                // Register the context in the scope so SubActions can access it
                var contextAccessor = scope.ServiceProvider.GetRequiredService<ISubActionExecutionContextAccessor>();
                contextAccessor.ExecutionContext = executionContext;

                _logger.LogDebug("Created SubAction execution context for action {ActionName} with LogId {LogId}", 
                    queuedAction.Action.Name, queuedAction.LogId);

                await actionService.RunAction(queuedAction.Variables, queuedAction.Action);

                Interlocked.Increment(ref _completedCount);

                _executionLogger.UpdateActionCompleted(queuedAction.LogId, queuedAction.Variables);

                _logger.LogDebug("Completed action {ActionName} in queue {QueueName}", 
                    queuedAction.Action.Name, Name);
            }
            catch (SubActionHandlerException subEx)
            {
                _executionLogger.UpdateActionFailed(queuedAction.LogId, $"Sub-action {subEx.SubActionType?.GetType().Name ?? "Unknown"} failed: {subEx.Message}", queuedAction.Variables);
                _logger.LogError("Sub-action failed while executing action {ActionName} in queue {QueueName}", 
                    queuedAction.Action.Name, Name);
            }
            catch (Exception ex)
            {
                _executionLogger.UpdateActionFailed(queuedAction.LogId, ex.Message, queuedAction.Variables);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _currentlyExecuting);

                // Notify clients about queue statistics change
                await SendQueueStatsUpdateAsync();
            }
        }

        private async Task SendQueueStatsUpdateAsync()
        {
            if (_hubContext == null) return;

            var stats = new QueueStatistics
            {
                QueueName = Name,
                PendingActions = PendingCount,
                CompletedActions = CompletedCount,
                IsBlocking = IsBlocking,
                IsEnabled = IsEnabled,
                MaxConcurrentActions = MaxConcurrentActions,
                CurrentlyExecuting = CurrentlyExecuting
            };

            await _hubContext.Clients.All.SendAsync("QueueStatsUpdated", stats);
        }

        private record QueuedAction(ActionType Action, ConcurrentDictionary<string, string> Variables, Guid LogId, Guid? ParentLogId, int? ParentSubActionIndex);
    }
}
