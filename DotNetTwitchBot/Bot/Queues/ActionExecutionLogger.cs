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

        public ActionExecutionLogger(
            ILogger<ActionExecutionLogger> logger, 
            IHubContext<MainHub> hubContext,
            int maxLogEntries = 1000)
        {
            _logger = logger;
            _hubContext = hubContext;
            _maxLogEntries = maxLogEntries;
        }

        public Guid LogActionEnqueued(string actionName, string queueName, Dictionary<string, string> variables)
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

            // Notify clients about new action log
            _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", log);

            return log.Id;
        }

        public void UpdateActionStarted(Guid logId)
        {
            if (_logIndex.TryGetValue(logId, out var log))
            {
                log.State = ActionExecutionState.Running;
                log.StartedAt = DateTime.UtcNow;

                _logger.LogTrace("Updated action {ActionName} to Running state", log.ActionName);

                // Notify clients about action update
                _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", log);
            }
        }

        public void UpdateActionCompleted(Guid logId, Dictionary<string, string> variablesAfter)
        {
            if (_logIndex.TryGetValue(logId, out var log))
            {
                log.State = ActionExecutionState.Completed;
                log.CompletedAt = DateTime.UtcNow;
                log.VariablesAfter = new Dictionary<string, string>(variablesAfter);

                _logger.LogTrace("Updated action {ActionName} to Completed state in {Duration}ms", 
                    log.ActionName, log.ExecutionDuration?.TotalMilliseconds ?? 0);

                // Notify clients about action completion
                _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", log);
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

                // Notify clients about action failure
                _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", log);
            }
        }

        public void UpdateActionFailed(Guid logId, string errorMessage,Dictionary<string, string> variablesAfter)
        {
            if (_logIndex.TryGetValue(logId, out var log))
            {
                log.State = ActionExecutionState.Failed;
                log.CompletedAt = DateTime.UtcNow;
                log.ErrorMessage = errorMessage;
                log.VariablesAfter = new Dictionary<string, string>(variablesAfter);

                _logger.LogTrace("Updated action {ActionName} to Failed state: {ErrorMessage}",
                    log.ActionName, errorMessage);

                // Notify clients about action failure
                _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", log);
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

                // Notify clients about action update
                _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", log);

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

                    // Notify clients about action update
                    _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", log);
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

                    // Notify clients about action update
                    _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", log);
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

                    // Only send updates every few messages to avoid flooding
                    if (messageCount % 5 == 0 || messageCount <= 5)
                    {
                        _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", log);
                    }
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

                // Notify clients about the update
                _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", parentLog);
            }
        }

        public void SetParentAction(Guid childLogId, Guid parentLogId)
        {
            if (_logIndex.TryGetValue(childLogId, out var childLog))
            {
                childLog.ParentActionLogId = parentLogId;

                _logger.LogTrace("Set parent {ParentLogId} for child action {ChildLogId}",
                    parentLogId, childLogId);

                // Notify clients about the update
                _ = _hubContext.Clients.All.SendAsync("ActionLogUpdated", childLog);
            }
        }
    }
}
