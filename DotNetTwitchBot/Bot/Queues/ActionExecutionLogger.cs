using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Bot.Models.Queues;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Queues
{
    public class ActionExecutionLogger : IActionExecutionLogger
    {
        private readonly ConcurrentQueue<ActionExecutionLog> _logs = new();
        private readonly ConcurrentDictionary<Guid, ActionExecutionLog> _logIndex = new();
        private readonly int _maxLogEntries;
        private readonly ILogger<ActionExecutionLogger> _logger;
        private readonly IHubContext<MainHub> _hubContext;

        // Throttling for SignalR updates
        private readonly Timer _updateTimer;
        private readonly ConcurrentDictionary<Guid, byte> _pendingLogUpdates = new();
        private readonly object _updateLock = new();
        private bool _updatePending;

        public ActionExecutionLogger(
            ILogger<ActionExecutionLogger> logger, 
            IHubContext<MainHub> hubContext,
            int maxLogEntries = 1000)
        {
            _logger = logger;
            _hubContext = hubContext;
            _maxLogEntries = maxLogEntries;

            // Initialize timer for throttled SignalR updates (max 4 updates per second)
            _updateTimer = new Timer(SendThrottledLogUpdates, null, Timeout.Infinite, Timeout.Infinite);
        }

        public Guid LogActionEnqueued(string actionName, string queueName, ConcurrentDictionary<string, string> variables)
        {
            var log = new ActionExecutionLog
            {
                ActionName = actionName,
                QueueName = queueName,
                VariablesBefore = new Dictionary<string, string>(variables),
                State = ActionExecutionState.Pending,
                EnqueuedAt = DateTime.UtcNow
            };

            _logs.Enqueue(log);
            _logIndex[log.Id] = log;

            EnforceMaxLogEntries();

            _logger.LogTrace("Logged action {ActionName} enqueued to {QueueName} with ID {LogId}", 
                actionName, queueName, log.Id);

            // Notify clients about new action log (send snapshot to prevent concurrent modification during serialization)
            _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", CreateLogSnapshot(log));

            return log.Id;
        }

        public void UpdateActionStarted(Guid logId)
        {
            if (_logIndex.TryGetValue(logId, out var log))
            {
                log.State = ActionExecutionState.Running;
                log.StartedAt = DateTime.UtcNow;

                _logger.LogTrace("Updated action {ActionName} to Running state", log.ActionName);

                // Notify clients about action update (send snapshot to prevent concurrent modification during serialization)
                _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", CreateLogSnapshot(log));
            }
        }

        public void UpdateActionCompleted(Guid logId, ConcurrentDictionary<string, string> variablesAfter)
        {
            if (_logIndex.TryGetValue(logId, out var log))
            {
                log.State = ActionExecutionState.Completed;
                log.CompletedAt = DateTime.UtcNow;
                log.VariablesAfter = new Dictionary<string, string>(variablesAfter);

                _logger.LogTrace("Updated action {ActionName} to Completed state in {Duration}ms", 
                    log.ActionName, log.ExecutionDuration?.TotalMilliseconds ?? 0);

                // Notify clients about action completion (send snapshot to prevent concurrent modification during serialization)
                _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", CreateLogSnapshot(log));
            }
        }

        public void UpdateActionFailed(Guid logId, string errorMessage)
        {
            if (_logIndex.TryGetValue(logId, out var log))
            {
                log.State = ActionExecutionState.Failed;
                log.CompletedAt = DateTime.UtcNow;
                log.ErrorMessage = errorMessage;

                _logger.LogTrace("Updated action {ActionName} to Failed state: {ErrorMessage}", 
                    log.ActionName, errorMessage);

                // Notify clients about action failure (send snapshot to prevent concurrent modification during serialization)
                _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", CreateLogSnapshot(log));
            }
        }

        public void UpdateActionFailed(Guid logId, string errorMessage, ConcurrentDictionary<string, string> variablesAfter)
        {
            if (_logIndex.TryGetValue(logId, out var log))
            {
                log.State = ActionExecutionState.Failed;
                log.CompletedAt = DateTime.UtcNow;
                log.ErrorMessage = errorMessage;
                log.VariablesAfter = new Dictionary<string, string>(variablesAfter);

                _logger.LogTrace("Updated action {ActionName} to Failed state: {ErrorMessage}",
                    log.ActionName, errorMessage);

                // Notify clients about action failure (send snapshot to prevent concurrent modification during serialization)
                _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", CreateLogSnapshot(log));
            }
        }

        public IReadOnlyList<ActionExecutionLog> GetRecentLogs(int count = 100)
        {
            return _logs.Reverse().Take(count).ToList();
        }

        public ActionExecutionLog? GetLogById(Guid logId)
        {
            return _logIndex.TryGetValue(logId, out var log) ? log : null;
        }

        /// <summary>
        /// Creates a thread-safe deep snapshot of the entire ActionExecutionLog for SignalR transmission.
        /// This prevents "Collection was modified" exceptions during SignalR serialization when
        /// concurrent threads are modifying SubActionLogs, Messages, or ChildActionLogIds.
        /// </summary>
        private ActionExecutionLog CreateLogSnapshot(ActionExecutionLog log)
        {
            var snapshot = new ActionExecutionLog
            {
                Id = log.Id,
                ActionName = log.ActionName,
                State = log.State,
                VariablesBefore = log.VariablesBefore,
                VariablesAfter = log.VariablesAfter,
                QueueName = log.QueueName,
                EnqueuedAt = log.EnqueuedAt,
                StartedAt = log.StartedAt,
                CompletedAt = log.CompletedAt,
                ErrorMessage = log.ErrorMessage,
                ParentActionLogId = log.ParentActionLogId
            };

            // Deep copy SubActionLogs with locks
            lock (log.SubActionLogs)
            {
                foreach (var subActionLog in log.SubActionLogs)
                {
                    List<string> messagesCopy;
                    lock (subActionLog.Messages)
                    {
                        messagesCopy = [.. subActionLog.Messages];
                    }

                    snapshot.SubActionLogs.Add(new SubActionExecutionLog
                    {
                        SubActionType = subActionLog.SubActionType,
                        Description = subActionLog.Description,
                        StartedAt = subActionLog.StartedAt,
                        CompletedAt = subActionLog.CompletedAt,
                        IsSuccess = subActionLog.IsSuccess,
                        ErrorMessage = subActionLog.ErrorMessage,
                        Messages = messagesCopy,
                        Depth = subActionLog.Depth,
                        ChildActionLogId = subActionLog.ChildActionLogId
                    });
                }
            }

            // Deep copy ChildActionLogIds with lock
            lock (log.ChildActionLogIds)
            {
                snapshot.ChildActionLogIds = [.. log.ChildActionLogIds];
            }

            return snapshot;
        }

        /// <summary>
        /// Creates a thread-safe deep snapshot of SubActionLogs for a given action log.
        /// This performs a deep copy including each SubActionExecutionLog and its Messages list.
        /// This prevents concurrent modification exceptions when the UI renders the list
        /// while background threads are modifying it.
        /// </summary>
        public List<SubActionExecutionLog> GetSubActionLogsSnapshot(Guid actionLogId)
        {
            if (_logIndex.TryGetValue(actionLogId, out var log))
            {
                lock (log.SubActionLogs)
                {
                    var snapshot = new List<SubActionExecutionLog>(log.SubActionLogs.Count);
                    foreach (var subActionLog in log.SubActionLogs)
                    {
                        List<string> messagesCopy;
                        lock (subActionLog.Messages)
                        {
                            messagesCopy = [.. subActionLog.Messages];
                        }

                        snapshot.Add(new SubActionExecutionLog
                        {
                            SubActionType = subActionLog.SubActionType,
                            Description = subActionLog.Description,
                            StartedAt = subActionLog.StartedAt,
                            CompletedAt = subActionLog.CompletedAt,
                            IsSuccess = subActionLog.IsSuccess,
                            ErrorMessage = subActionLog.ErrorMessage,
                            Messages = messagesCopy,
                            Depth = subActionLog.Depth,
                            ChildActionLogId = subActionLog.ChildActionLogId
                        });
                    }
                    return snapshot;
                }
            }
            return [];
        }

        /// <summary>
        /// Creates a thread-safe snapshot of messages for a specific SubAction.
        /// This prevents concurrent modification exceptions when the UI renders messages
        /// while background threads are adding new ones.
        /// </summary>
        public List<string> GetSubActionMessagesSnapshot(Guid actionLogId, int subActionIndex)
        {
            if (_logIndex.TryGetValue(actionLogId, out var log))
            {
                SubActionExecutionLog? subActionLog = null;
                lock (log.SubActionLogs)
                {
                    if (subActionIndex >= 0 && subActionIndex < log.SubActionLogs.Count)
                    {
                        subActionLog = log.SubActionLogs[subActionIndex];
                    }
                }

                if (subActionLog != null)
                {
                    lock (subActionLog.Messages)
                    {
                        return [.. subActionLog.Messages];
                    }
                }
            }
            return [];
        }

        public IReadOnlyList<ActionExecutionLog> GetLogsByQueue(string queueName, int count = 100)
        {
            return _logs
                .Where(log => log.QueueName.Equals(queueName, StringComparison.OrdinalIgnoreCase))
                .Reverse()
                .Take(count)
                .ToList();
        }

        public IReadOnlyList<ActionExecutionLog> GetLogsByState(ActionExecutionState state, int count = 100)
        {
            return _logs
                .Where(log => log.State == state)
                .Reverse()
                .Take(count)
                .ToList();
        }

        public IReadOnlyList<ActionExecutionLog> GetLogs(DateTime since)
        {
            return _logs
                .Where(log => log.EnqueuedAt >= since)
                .Reverse()
                .ToList();
        }

        public void Clear()
        {
            _logs.Clear();
            _logIndex.Clear();
            _logger.LogInformation("Cleared all action execution logs");
        }

        public int GetLogCount()
        {
            return _logs.Count;
        }

        public int MaxLogCount()
        {
            return _maxLogEntries;
        }

        public int LogSubActionStarted(Guid actionLogId, string subActionType, string? description, int depth)
        {
            if (_logIndex.TryGetValue(actionLogId, out var log))
            {
                var subActionLog = new SubActionExecutionLog
                {
                    SubActionType = subActionType,
                    Description = description,
                    StartedAt = DateTime.UtcNow,
                    Depth = depth
                };

                // Thread-safe add to list
                int index;
                lock (log.SubActionLogs)
                {
                    log.SubActionLogs.Add(subActionLog);
                    index = log.SubActionLogs.Count - 1;
                }

                _logger.LogTrace("Logged sub-action {SubActionType} started for action {ActionName} at index {Index}", 
                    subActionType, log.ActionName, index);

                // Schedule throttled SignalR update
                ScheduleLogUpdate(actionLogId);

                return index;
            }

            return -1;
        }

        public void LogSubActionCompleted(Guid actionLogId, int subActionIndex)
        {
            if (_logIndex.TryGetValue(actionLogId, out var log))
            {
                SubActionExecutionLog? subActionLog = null;
                lock (log.SubActionLogs)
                {
                    if (subActionIndex >= 0 && subActionIndex < log.SubActionLogs.Count)
                    {
                        subActionLog = log.SubActionLogs[subActionIndex];
                    }
                }

                if (subActionLog != null)
                {
                    subActionLog.CompletedAt = DateTime.UtcNow;
                    subActionLog.IsSuccess = true;

                    _logger.LogTrace("Sub-action {SubActionType} completed for action {ActionName} in {Duration}ms", 
                        subActionLog.SubActionType, log.ActionName, subActionLog.Duration?.TotalMilliseconds ?? 0);

                    // Schedule throttled SignalR update
                    ScheduleLogUpdate(actionLogId);
                }
            }
        }

        public void LogSubActionFailed(Guid actionLogId, int subActionIndex, string errorMessage)
        {
            if (_logIndex.TryGetValue(actionLogId, out var log))
            {
                SubActionExecutionLog? subActionLog = null;
                lock (log.SubActionLogs)
                {
                    if (subActionIndex >= 0 && subActionIndex < log.SubActionLogs.Count)
                    {
                        subActionLog = log.SubActionLogs[subActionIndex];
                    }
                }

                if (subActionLog != null)
                {
                    subActionLog.CompletedAt = DateTime.UtcNow;
                    subActionLog.IsSuccess = false;
                    subActionLog.ErrorMessage = TruncateMessage(errorMessage, 200);

                    _logger.LogTrace("Sub-action {SubActionType} failed for action {ActionName}: {ErrorMessage}", 
                        subActionLog.SubActionType, log.ActionName, errorMessage);

                    // Schedule throttled SignalR update
                    ScheduleLogUpdate(actionLogId);
                }
            }
        }

        public void LogSubActionMessage(Guid actionLogId, int subActionIndex, string message)
        {
            if (_logIndex.TryGetValue(actionLogId, out var log))
            {
                SubActionExecutionLog? subActionLog = null;
                lock (log.SubActionLogs)
                {
                    if (subActionIndex >= 0 && subActionIndex < log.SubActionLogs.Count)
                    {
                        subActionLog = log.SubActionLogs[subActionIndex];
                    }
                }

                if (subActionLog != null)
                {
                    var truncatedMessage = TruncateMessage(message, 150);

                    int messageCount;
                    lock (subActionLog.Messages)
                    {
                        subActionLog.Messages.Add(truncatedMessage);
                        messageCount = subActionLog.Messages.Count;
                    }

                    _logger.LogTrace("Sub-action {SubActionType} logged message for action {ActionName}: {Message}", 
                        subActionLog.SubActionType, log.ActionName, truncatedMessage);

                    // Schedule throttled SignalR update (throttling replaces the old "every 5 messages" logic)
                    ScheduleLogUpdate(actionLogId);
                }
            }
        }

        private static string TruncateMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
                return message;

            return message[..(maxLength - 3)] + "...";
        }

        private void EnforceMaxLogEntries()
        {
            while (_logs.Count > _maxLogEntries)
            {
                if (_logs.TryDequeue(out var removedLog))
                {
                    _logIndex.TryRemove(removedLog.Id, out _);
                    _logger.LogTrace("Removed old log entry {LogId} for action {ActionName}", 
                        removedLog.Id, removedLog.ActionName);
                }
            }
        }

        public void LinkChildAction(Guid parentLogId, int subActionIndex, Guid childLogId)
        {
            if (_logIndex.TryGetValue(parentLogId, out var parentLog))
            {
                // Add to parent's child list
                lock (parentLog.ChildActionLogIds)
                {
                    if (!parentLog.ChildActionLogIds.Contains(childLogId))
                    {
                        parentLog.ChildActionLogIds.Add(childLogId);
                    }
                }

                // Link the SubAction to the child action
                SubActionExecutionLog? subActionLog = null;
                lock (parentLog.SubActionLogs)
                {
                    if (subActionIndex >= 0 && subActionIndex < parentLog.SubActionLogs.Count)
                    {
                        subActionLog = parentLog.SubActionLogs[subActionIndex];
                    }
                }

                if (subActionLog != null)
                {
                    subActionLog.ChildActionLogId = childLogId;
                }

                _logger.LogTrace("Linked child action {ChildLogId} to parent {ParentLogId} at SubAction index {Index}",
                    childLogId, parentLogId, subActionIndex);

                // Schedule throttled SignalR update
                ScheduleLogUpdate(parentLogId);
            }
        }

        public void SetParentAction(Guid childLogId, Guid parentLogId)
        {
            if (_logIndex.TryGetValue(childLogId, out var childLog))
            {
                childLog.ParentActionLogId = parentLogId;

                _logger.LogTrace("Set parent {ParentLogId} for child action {ChildLogId}",
                    parentLogId, childLogId);

                // Schedule throttled SignalR update
                ScheduleLogUpdate(childLogId);
            }
        }

        /// <summary>
        /// Schedules a throttled SignalR update for a specific log.
        /// Updates are batched and sent at most every 250ms (4 times per second).
        /// </summary>
        private void ScheduleLogUpdate(Guid logId)
        {
            // Add log to pending updates set
            _pendingLogUpdates.TryAdd(logId, 0);

            // Schedule the timer if not already scheduled
            lock (_updateLock)
            {
                if (!_updatePending)
                {
                    _updatePending = true;
                    // Schedule update to run after 250ms (max 4 updates/second)
                    _updateTimer.Change(250, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        /// Timer callback that sends all pending log updates via SignalR.
        /// This is called at most every 250ms to batch updates and reduce SignalR traffic.
        /// </summary>
        private void SendThrottledLogUpdates(object? state)
        {
            lock (_updateLock)
            {
                _updatePending = false;
            }

            // Get all pending log IDs and clear the set
            var pendingLogIds = _pendingLogUpdates.Keys.ToArray();
            _pendingLogUpdates.Clear();

            // Send updates for all pending logs
            foreach (var logId in pendingLogIds)
            {
                if (_logIndex.TryGetValue(logId, out var log))
                {
                    // Fire and forget - we don't need to await this
                    _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", CreateLogSnapshot(log));
                }
            }

        }

        /// <summary>
        /// Disposes the timer used for throttling SignalR updates.
        /// Should be called when the logger is being disposed.
        /// </summary>
        public void Dispose()
        {
            _updateTimer?.Dispose();
        }
    }
}
