using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Database.Bot.Models.Queues;
using PenguinTwitchBot.Bot.WebSocketEvents;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Diagnostics;

namespace PenguinTwitchBot.Bot.Queues
{
    public class ActionQueue : IActionQueue
    {
        private static readonly TimeSpan SlowActionThreshold = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ActionWarningTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PressureDeferralInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan MaxPressureDeferral = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan EnqueueTimeout = TimeSpan.FromSeconds(30);
        private const int LowWorkerThreadThreshold = 20;
        private readonly Channel<QueuedAction> _channel;
        private readonly ILogger<ActionQueue> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IActionExecutionLogger _executionLogger;
        private readonly IHubContext<MainHub>? _hubContext;
        private readonly IWsEventHandler _wsEventHandler;
        private readonly SemaphoreSlim _semaphore;
        private readonly GlobalConcurrencyLimiter? _globalLimiter;
        private readonly int _maxPendingActions;
        private readonly int _pendingWarningThreshold;
        private DateTime _lastPendingWarningUtc = DateTime.MinValue;
        private DateTime _lastPressureWarningUtc = DateTime.MinValue;
        private long _completedCount;
        private int _currentlyExecuting;
        private int _pendingCount;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _processingTask;
        private readonly object _startLock = new();

        // Throttling for SignalR updates
        private readonly Timer _statsUpdateTimer;
        private bool _statsPendingUpdate;
        private readonly object _statsUpdateLock = new();

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
            IHubContext<MainHub>? hubContext = null,
            GlobalConcurrencyLimiter? globalLimiter = null)
        {
            _globalLimiter = globalLimiter;
            Name = name;
            IsBlocking = isBlocking;
            MaxConcurrentActions = maxConcurrentActions;
            IsEnabled = true;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _executionLogger = executionLogger;
            _hubContext = hubContext;
            _wsEventHandler = wsEventHandler;

            // Keep queue growth bounded to avoid unbounded memory growth and runaway latency.
            _maxPendingActions = Math.Max(maxConcurrentActions * 200, 1000);
            _pendingWarningThreshold = (int)Math.Ceiling(_maxPendingActions * 0.8);

            var channelOptions = new BoundedChannelOptions(_maxPendingActions)
            {
                SingleReader = isBlocking,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            };
            _channel = Channel.CreateBounded<QueuedAction>(channelOptions);
            _semaphore = new SemaphoreSlim(isBlocking ? 1 : maxConcurrentActions);

            // Initialize timer for throttled SignalR updates (max 4 updates per second)
            _statsUpdateTimer = new Timer(SendThrottledStatsUpdate, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task EnqueueAsync(ActionType action, ConcurrentDictionary<string, string> variables, Guid? parentLogId = null, int? parentSubActionIndex = null)
        {
            if (!IsEnabled)
            {
                _logger.LogWarning("Queue {QueueName} is disabled, action {ActionName} not enqueued", Name, action.Name);
                return;
            }

            var logId = _executionLogger.LogActionEnqueued(action.Name, action.Id, Name, variables);
            var queuedAction = new QueuedAction(action, variables, logId, parentLogId, parentSubActionIndex);

            // Treat non-blocking queues as lower priority under pool pressure and defer enqueue briefly.
            if (!IsBlocking)
            {
                await DeferForThreadPressureAsync(action.Name);
            }

            var pendingBeforeEnqueue = Volatile.Read(ref _pendingCount);
            if (pendingBeforeEnqueue >= _pendingWarningThreshold)
            {
                var now = DateTime.UtcNow;
                if (now - _lastPendingWarningUtc >= TimeSpan.FromSeconds(30))
                {
                    _lastPendingWarningUtc = now;
                    _logger.LogWarning(
                        "Queue {QueueName} is under pressure. Pending actions: {PendingCount}/{MaxPendingActions} (executing: {CurrentlyExecuting}, maxConcurrent: {MaxConcurrentActions})",
                        Name,
                        pendingBeforeEnqueue,
                        _maxPendingActions,
                        _currentlyExecuting,
                        MaxConcurrentActions);
                }
            }

            // Increment before WriteAsync: the consumer can dequeue and decrement _pendingCount
            // before the post-WriteAsync continuation runs, which would make the count go negative.
            Interlocked.Increment(ref _pendingCount);
            try
            {
                // Use a timeout so producers can't block indefinitely when the channel is saturated.
                using var cts = new CancellationTokenSource(EnqueueTimeout);
                await _channel.Writer.WriteAsync(queuedAction, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Decrement(ref _pendingCount);
                _logger.LogError(
                    "Queue {QueueName} is saturated - action {ActionName} dropped after {TimeoutMs}ms wait.",
                    Name, action.Name, EnqueueTimeout.TotalMilliseconds);
                _executionLogger.UpdateActionFailed(logId, "Enqueue timed out: queue is saturated.", variables);
                return;
            }
            catch
            {
                Interlocked.Decrement(ref _pendingCount);
                throw;
            }
            _logger.LogDebug("Action {ActionName} enqueued to {QueueName}", action.Name, Name);

            SendEventToWs(queuedAction);

            // Notify clients about queue statistics change
            await SendQueueStatsUpdateAsync();
        }

        private async Task DeferForThreadPressureAsync(string actionName)
        {
            var deferStart = DateTime.UtcNow;
            while (IsThreadPoolUnderPressure())
            {
                var now = DateTime.UtcNow;
                if (now - _lastPressureWarningUtc >= TimeSpan.FromSeconds(30))
                {
                    _lastPressureWarningUtc = now;
                    ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIocp);
                    ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIocp);
                    _logger.LogWarning(
                        "Deferring low-priority queue enqueue for {QueueName}/{ActionName} due to thread pressure. WorkerThreads={AvailableWorkers}/{MaxWorkers}, IOCP={AvailableIocp}/{MaxIocp}",
                        Name,
                        actionName,
                        availableWorkers,
                        maxWorkers,
                        availableIocp,
                        maxIocp);
                }

                if (DateTime.UtcNow - deferStart >= MaxPressureDeferral)
                {
                    _logger.LogWarning(
                        "Proceeding with enqueue for {QueueName}/{ActionName} after max deferral window {DeferralMs}ms.",
                        Name,
                        actionName,
                        MaxPressureDeferral.TotalMilliseconds);
                    break;
                }

                await Task.Delay(PressureDeferralInterval);
            }
        }

        private static bool IsThreadPoolUnderPressure()
        {
            ThreadPool.GetAvailableThreads(out var availableWorkers, out _);
            return availableWorkers <= LowWorkerThreadThreshold;
        }

        private void SendEventToWs(QueuedAction queuedAction)
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

            // Fire-and-forget: Don't block enqueue operation for WebSocket notifications
            var task = _wsEventHandler.AddToQueue(wsEvent);
            if (!task.IsCompleted)
            {
                _ = task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger.LogWarning(t.Exception, "Failed to send WebSocket event for action {ActionName} in queue {QueueName}", 
                            queuedAction.Action.Name, Name);
                    }
                }, TaskScheduler.Default);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            lock (_startLock)
            {
                if (_processingTask != null && !_processingTask.IsCompleted)
                {
                    return Task.CompletedTask;
                }

                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                _processingTask = IsBlocking
                    ? ProcessBlockingQueueAsync(_cancellationTokenSource.Token)
                    : ProcessNonBlockingQueueAsync(_cancellationTokenSource.Token);
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

            // Stop and dispose the stats update timer
            await _statsUpdateTimer.DisposeAsync();

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
                    if (_globalLimiter != null)
                    {
                        try
                        {
                            await _globalLimiter.WaitAsync(cancellationToken);
                        }
                        catch
                        {
                            // Release the per-queue permit if we can't acquire the global one.
                            _semaphore.Release();
                            throw;
                        }
                    }

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
                            _globalLimiter?.Release();
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
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _currentlyExecuting);
            Interlocked.Decrement(ref _pendingCount);
            ActionExecutionContext? executionContext = null;

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

                // Create the execution context for this action - this is passed explicitly through all execution layers
                var contextLogger = scope.ServiceProvider.GetRequiredService<ILogger<ActionExecutionContext>>();
                executionContext = new ActionExecutionContext(queuedAction.LogId, _executionLogger, contextLogger);

                _logger.LogDebug("Created execution context for action {ActionName} with LogId {LogId}", 
                    queuedAction.Action.Name, queuedAction.LogId);

                // Pass the context explicitly to RunAction
                var actionTask = actionService.RunAction(queuedAction.Variables, queuedAction.Action, executionContext);
                var warnedNoProgress = false;
                while (!actionTask.IsCompleted)
                {
                    // Use CancellationToken.None so shutdown cancellation does not complete
                    // this delay early and trigger misleading stuck-action diagnostics.
                    var warningTask = Task.Delay(ActionWarningTimeout, CancellationToken.None);
                    var firstCompleted = await Task.WhenAny(actionTask, warningTask);
                    if (firstCompleted == actionTask)
                    {
                        break;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (executionContext.IsIntentionalDelayInProgress)
                    {
                        warnedNoProgress = false;
                        var remainingDelay = executionContext.GetIntentionalDelayRemaining();
                        _logger.LogDebug(
                            "Skipping long-running warning for queue {QueueName}: {ActionName} is in intentional delay. Remaining delay: {RemainingDelayMs}ms",
                            Name,
                            queuedAction.Action.Name,
                            (long)remainingDelay.TotalMilliseconds);
                        continue;
                    }

                    var idleFor = executionContext.TimeSinceProgress;
                    if (idleFor < ActionWarningTimeout)
                    {
                        warnedNoProgress = false;
                        continue;
                    }

                    if (!warnedNoProgress)
                    {
                        ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIocp);
                        ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIocp);
                        _logger.LogWarning(
                            "Long-running action detected in queue {QueueName}: {ActionName} had no progress for {IdleForMs}ms (threshold {TimeoutMs}ms). WorkerThreads={AvailableWorkers}/{MaxWorkers}, IOCP={AvailableIocp}/{MaxIocp}, pending={PendingCount}, executing={CurrentlyExecuting}",
                            Name,
                            queuedAction.Action.Name,
                            (long)idleFor.TotalMilliseconds,
                            ActionWarningTimeout.TotalMilliseconds,
                            availableWorkers,
                            maxWorkers,
                            availableIocp,
                            maxIocp,
                            _pendingCount,
                            _currentlyExecuting);
                        warnedNoProgress = true;
                    }
                }

                await actionTask;

                stopwatch.Stop();
                var intentionalDelay = executionContext.GetCompletedIntentionalDelay();
                var effectiveElapsed = stopwatch.Elapsed - intentionalDelay;
                if (effectiveElapsed < TimeSpan.Zero)
                {
                    effectiveElapsed = TimeSpan.Zero;
                }

                if (effectiveElapsed >= SlowActionThreshold)
                {
                    _logger.LogWarning(
                        "Slow action detected in queue {QueueName}: {ActionName} effective runtime {EffectiveElapsedMs}ms (raw: {ElapsedMs}ms, intentional delay: {IntentionalDelayMs}ms, pending: {PendingCount}, executing: {CurrentlyExecuting})",
                        Name,
                        queuedAction.Action.Name,
                        effectiveElapsed.TotalMilliseconds,
                        stopwatch.ElapsedMilliseconds,
                        intentionalDelay.TotalMilliseconds,
                        _pendingCount,
                        _currentlyExecuting);
                }

                Interlocked.Increment(ref _completedCount);

                _executionLogger.UpdateActionCompleted(queuedAction.LogId, queuedAction.Variables);

                _logger.LogDebug("Completed action {ActionName} in queue {QueueName}", 
                    queuedAction.Action.Name, Name);
            }
            catch (SubActionUserFacingException userFacingEx)
            {
                var failureMessage = BuildSubActionFailureMessage(userFacingEx);
                queuedAction.Variables[ActionExecutionVariableKeys.ActionErrorMessage] = userFacingEx.Message;
                _executionLogger.UpdateActionFailed(queuedAction.LogId, failureMessage, queuedAction.Variables);
                await RunCatchSubActionsAsync(queuedAction, executionContext);

                _logger.LogError(userFacingEx, "User-facing sub-action failed while executing action {ActionName} in queue {QueueName}",
                    queuedAction.Action.Name, Name);
            }
            catch (SubActionHandlerException subEx)
            {
                var failureMessage = BuildSubActionFailureMessage(subEx);
                _executionLogger.UpdateActionFailed(queuedAction.LogId, failureMessage, queuedAction.Variables);

                _logger.LogError(subEx, "Sub-action failed while executing action {ActionName} in queue {QueueName}",
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

        private async Task RunCatchSubActionsAsync(QueuedAction queuedAction, ActionExecutionContext? executionContext)
        {
            var catchSubActions = (queuedAction.Action.CatchSubActions ?? [])
                .Where(x => x.Enabled)
                .ToList();

            if (catchSubActions.Count == 0)
            {
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var actionService = scope.ServiceProvider.GetRequiredService<Actions.IAction>();

            var catchAction = new ActionType
            {
                Name = $"{queuedAction.Action.Name} (Catch)",
                Enabled = true,
                ConcurrentAction = queuedAction.Action.ConcurrentAction,
                QueueName = queuedAction.Action.QueueName,
                SubActions = catchSubActions
            };

            await actionService.RunAction(queuedAction.Variables, catchAction, executionContext);
        }

        private static string BuildSubActionFailureMessage(SubActionHandlerException subEx)
        {
            var subActionName = subEx.SubActionType?.SubActionTypes.ToString() ?? "Unknown";
            var argsText = subEx.Args.Length == 0
                ? "none"
                : string.Join(", ", subEx.Args.Select(a => a?.ToString() ?? "null"));

            return $"Sub-action {subActionName} failed: {subEx.Message} | Args: {argsText}";
        }

        private Task SendQueueStatsUpdateAsync()
        {
            if (_hubContext == null) return Task.CompletedTask;

            // Instead of sending immediately, schedule a throttled update
            lock (_statsUpdateLock)
            {
                if (!_statsPendingUpdate)
                {
                    _statsPendingUpdate = true;
                    // Schedule update to run after 250ms (max 4 updates/second)
                    _statsUpdateTimer.Change(250, Timeout.Infinite);
                }
            }

            return Task.CompletedTask;
        }

        private void SendThrottledStatsUpdate(object? state)
        {
            lock (_statsUpdateLock)
            {
                _statsPendingUpdate = false;
            }

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

            // Fire and forget - we don't need to await this
            _ = _hubContext.Clients.All.SendAsync("QueueStatsUpdated", stats);
        }

        private record QueuedAction(ActionType Action, ConcurrentDictionary<string, string> Variables, Guid LogId, Guid? ParentLogId, int? ParentSubActionIndex);
    }
}
