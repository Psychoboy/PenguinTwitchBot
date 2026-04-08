namespace DotNetTwitchBot.Bot.Queues
{
    public interface ISubActionExecutionContext
    {
        Guid ActionLogId { get; }
        int CurrentSubActionIndex { get; }
        int Depth { get; }
        
        void BeginSubAction(string subActionType, string? description = null);
        void CompleteSubAction();
        void FailSubAction(string errorMessage);
        void LogMessage(string message);
        ISubActionExecutionContext CreateNestedContext();
    }

    public class SubActionExecutionContext : ISubActionExecutionContext
    {
        private readonly IActionExecutionLogger _executionLogger;
        private readonly ILogger<SubActionExecutionContext> _logger;

        public Guid ActionLogId { get; }
        public int CurrentSubActionIndex { get; private set; } = -1;
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

        public void BeginSubAction(string subActionType, string? description = null)
        {
            CurrentSubActionIndex = _executionLogger.LogSubActionStarted(ActionLogId, subActionType, description, Depth);
            _logger.LogDebug("SubAction {SubActionType} started at depth {Depth} with index {Index}", subActionType, Depth, CurrentSubActionIndex);
        }

        public void CompleteSubAction()
        {
            if (CurrentSubActionIndex >= 0)
            {
                _executionLogger.LogSubActionCompleted(ActionLogId, CurrentSubActionIndex);
                _logger.LogDebug("SubAction completed at index {Index}", CurrentSubActionIndex);
            }
        }

        public void FailSubAction(string errorMessage)
        {
            if (CurrentSubActionIndex >= 0)
            {
                _executionLogger.LogSubActionFailed(ActionLogId, CurrentSubActionIndex, errorMessage);
                _logger.LogDebug("SubAction failed at index {Index}: {Error}", CurrentSubActionIndex, errorMessage);
            }
        }

        public void LogMessage(string message)
        {
            if (CurrentSubActionIndex >= 0)
            {
                _executionLogger.LogSubActionMessage(ActionLogId, CurrentSubActionIndex, message);
                
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
