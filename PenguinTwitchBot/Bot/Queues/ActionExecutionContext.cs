namespace PenguinTwitchBot.Bot.Queues
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
        private long _lastProgressUtcTicks = DateTime.UtcNow.Ticks;
        private int _intentionalDelayCount;
        private long _intentionalDelayUntilUtcTicks;

        /// <summary>
        /// Unique identifier for this action execution instance
        /// </summary>
        public Guid ActionLogId { get; }

        /// <summary>
        /// The depth level of this context (0 for top-level actions, incremented for nested actions)
        /// </summary>
        public int Depth => _depth;
        public bool IsIntentionalDelayInProgress => Volatile.Read(ref _intentionalDelayCount) > 0;

        public TimeSpan TimeSinceProgress
        {
            get
            {
                var lastTicks = Volatile.Read(ref _lastProgressUtcTicks);
                return DateTime.UtcNow - new DateTime(lastTicks, DateTimeKind.Utc);
            }
        }

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
            TouchProgress();
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
            TouchProgress();
            _logger.LogSubActionMessage(ActionLogId, subActionIndex, message);
            _contextLogger.LogTrace("SubAction message at index {Index} for action {ActionLogId}: {Message}", 
                subActionIndex, ActionLogId, message);
        }

        /// <summary>
        /// Marks a subaction as completed
        /// </summary>
        public void CompleteSubAction(int subActionIndex)
        {
            TouchProgress();
            _logger.LogSubActionCompleted(ActionLogId, subActionIndex);
            _contextLogger.LogTrace("SubAction completed at index {Index} for action {ActionLogId}", 
                subActionIndex, ActionLogId);
        }

        /// <summary>
        /// Marks a subaction as failed with an error message
        /// </summary>
        public void FailSubAction(int subActionIndex, string errorMessage)
        {
            TouchProgress();
            _logger.LogSubActionFailed(ActionLogId, subActionIndex, errorMessage);
            _contextLogger.LogTrace("SubAction failed at index {Index} for action {ActionLogId}: {Error}", 
                subActionIndex, ActionLogId, errorMessage);
        }

        /// <summary>
        /// Links a child action to a parent subaction
        /// </summary>
        public void LinkChildAction(int subActionIndex, Guid childActionLogId)
        {
            TouchProgress();
            _logger.LinkChildAction(ActionLogId, subActionIndex, childActionLogId);
            _contextLogger.LogTrace("Child action {ChildActionLogId} linked to subaction at index {Index} for action {ActionLogId}", 
                childActionLogId, subActionIndex, ActionLogId);
        }

        public void BeginIntentionalDelay(TimeSpan duration)
        {
            TouchProgress();
            Interlocked.Increment(ref _intentionalDelayCount);

            var until = DateTime.UtcNow.Add(duration).Ticks;
            while (true)
            {
                var current = Volatile.Read(ref _intentionalDelayUntilUtcTicks);
                if (until <= current)
                {
                    break;
                }

                if (Interlocked.CompareExchange(ref _intentionalDelayUntilUtcTicks, until, current) == current)
                {
                    break;
                }
            }
        }

        public void EndIntentionalDelay()
        {
            TouchProgress();
            if (Interlocked.Decrement(ref _intentionalDelayCount) < 0)
            {
                Interlocked.Exchange(ref _intentionalDelayCount, 0);
            }
        }

        public TimeSpan GetIntentionalDelayRemaining()
        {
            if (!IsIntentionalDelayInProgress)
            {
                return TimeSpan.Zero;
            }

            var untilTicks = Volatile.Read(ref _intentionalDelayUntilUtcTicks);
            var remaining = new DateTime(untilTicks, DateTimeKind.Utc) - DateTime.UtcNow;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        /// <summary>
        /// Creates a child context for nested action execution (increments depth)
        /// </summary>
        public ActionExecutionContext CreateChildContext(Guid childActionLogId)
        {
            return new ActionExecutionContext(childActionLogId, _logger, _contextLogger, _depth + 1);
        }

        private void TouchProgress()
        {
            Interlocked.Exchange(ref _lastProgressUtcTicks, DateTime.UtcNow.Ticks);
        }
    }
}
