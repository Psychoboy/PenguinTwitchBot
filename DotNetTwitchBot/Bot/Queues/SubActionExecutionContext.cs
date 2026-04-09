namespace DotNetTwitchBot.Bot.Queues
{
    public interface ISubActionExecutionContext
    {
        Guid ActionLogId { get; }
        int Depth { get; }

        int BeginSubAction(string subActionType, string? description = null);
        void CompleteSubAction(int subActionIndex);
        void FailSubAction(int subActionIndex, string errorMessage);
        void LogMessage(int subActionIndex, string message);
        ISubActionExecutionContext CreateNestedContext();
    }

    public class SubActionExecutionContext : ISubActionExecutionContext
    {
        private readonly IActionExecutionLogger _executionLogger;
        private readonly ILogger<SubActionExecutionContext> _logger;

        public Guid ActionLogId { get; }
        public int Depth { get; }

        public SubActionExecutionContext(
            Guid actionLogId,
            IActionExecutionLogger executionLogger,
            ILogger<SubActionExecutionContext> logger,
            int depth = 0)
        {
            ActionLogId = actionLogId;
            _executionLogger = executionLogger;
            _logger = logger;
            Depth = depth;
        }

        public int BeginSubAction(string subActionType, string? description = null)
        {
            int index = _executionLogger.LogSubActionStarted(ActionLogId, subActionType, description, Depth);
            _logger.LogDebug("SubAction {SubActionType} started at depth {Depth} with index {Index}", subActionType, Depth, index);
            return index;
        }

        public void CompleteSubAction(int subActionIndex)
        {
            if (subActionIndex >= 0)
            {
                _executionLogger.LogSubActionCompleted(ActionLogId, subActionIndex);
                _logger.LogDebug("SubAction completed at index {Index}", subActionIndex);
            }
        }

        public void FailSubAction(int subActionIndex, string errorMessage)
        {
            if (subActionIndex >= 0)
            {
                _executionLogger.LogSubActionFailed(ActionLogId, subActionIndex, errorMessage);
                _logger.LogWarning("SubAction failed at index {Index}: {Error}", subActionIndex, errorMessage);
            }
        }

        public void LogMessage(int subActionIndex, string message)
        {
            if (subActionIndex >= 0)
            {
                _executionLogger.LogSubActionMessage(ActionLogId, subActionIndex, message);

                // Also log to standard logger for local debugging
                _logger.LogDebug("SubAction message: {Message}", message);
            }
        }

        public ISubActionExecutionContext CreateNestedContext()
        {
            return new SubActionExecutionContext(ActionLogId, _executionLogger, _logger, Depth + 1);
        }
    }
}
