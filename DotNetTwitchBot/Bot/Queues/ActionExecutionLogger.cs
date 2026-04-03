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

        public IReadOnlyList<ActionExecutionLog> GetRecentLogs(int count = 100)
        {
            return _logs.Reverse().Take(count).ToList();
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
    }
}
