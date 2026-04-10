using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Queues
{
    /// <summary>
    /// Execution context that tracks the execution of an action and its subactions.
    /// This context is created at the start of action execution and passed explicitly through all execution layers.
    /// </summary>
    public class ActionExecutionContext
    {
        private readonly IActionExecutionLogger _logger;
        private readonly ILogger<ActionExecutionContext> _contextLogger;
        private readonly int _depth;

        /// <summary>
        /// Unique identifier for this action execution instance
        /// </summary>
        public Guid ActionLogId { get; }

        /// <summary>
        /// The depth level of this context (0 for top-level actions, incremented for nested actions)
        /// </summary>
        public int Depth => _depth;

        /// <summary>
        /// Creates a new execution context with a generated ID
        /// </summary>
        public ActionExecutionContext(IActionExecutionLogger logger, ILogger<ActionExecutionContext> contextLogger, int depth = 0)
        {
            ActionLogId = Guid.NewGuid();
            _logger = logger;
            _contextLogger = contextLogger;
            _depth = depth;
        }

        /// <summary>
        /// Creates a new execution context with a specific ID (used when the action was already logged)
        /// </summary>
        public ActionExecutionContext(Guid actionLogId, IActionExecutionLogger logger, ILogger<ActionExecutionContext> contextLogger, int depth = 0)
        {
            ActionLogId = actionLogId;
            _logger = logger;
            _contextLogger = contextLogger;
            _depth = depth;
        }

        /// <summary>
        /// Logs the start of a subaction and returns the actual logged index
        /// The logger auto-assigns the index based on the list, so we must use that returned value
        /// </summary>
        public int BeginSubAction(int subActionIndex, string subActionType, string? description)
        {
            var actualIndex = _logger.LogSubActionStarted(ActionLogId, subActionType, description, _depth);
            _contextLogger.LogTrace("SubAction {SubActionType} started at index {ActualIndex} (requested {RequestedIndex}, depth {Depth}) for action {ActionLogId}", 
                subActionType, actualIndex, subActionIndex, _depth, ActionLogId);
            return actualIndex;
        }

        /// <summary>
        /// Logs a message for a specific subaction
        /// </summary>
        public void LogMessage(int subActionIndex, string message)
        {
            _logger.LogSubActionMessage(ActionLogId, subActionIndex, message);
            _contextLogger.LogTrace("SubAction message at index {Index} for action {ActionLogId}: {Message}", 
                subActionIndex, ActionLogId, message);
        }

        /// <summary>
        /// Marks a subaction as completed
        /// </summary>
        public void CompleteSubAction(int subActionIndex)
        {
            _logger.LogSubActionCompleted(ActionLogId, subActionIndex);
            _contextLogger.LogTrace("SubAction completed at index {Index} for action {ActionLogId}", 
                subActionIndex, ActionLogId);
        }

        /// <summary>
        /// Marks a subaction as failed with an error message
        /// </summary>
        public void FailSubAction(int subActionIndex, string errorMessage)
        {
            _logger.LogSubActionFailed(ActionLogId, subActionIndex, errorMessage);
            _contextLogger.LogTrace("SubAction failed at index {Index} for action {ActionLogId}: {Error}", 
                subActionIndex, ActionLogId, errorMessage);
        }

        /// <summary>
        /// Links a child action to a parent subaction
        /// </summary>
        public void LinkChildAction(int subActionIndex, Guid childActionLogId)
        {
            _logger.LinkChildAction(ActionLogId, subActionIndex, childActionLogId);
            _contextLogger.LogTrace("Child action {ChildActionLogId} linked to subaction at index {Index} for action {ActionLogId}", 
                childActionLogId, subActionIndex, ActionLogId);
        }

        /// <summary>
        /// Creates a child context for nested action execution (increments depth)
        /// </summary>
        public ActionExecutionContext CreateChildContext(Guid childActionLogId)
        {
            return new ActionExecutionContext(childActionLogId, _logger, _contextLogger, _depth + 1);
        }
    }
}
