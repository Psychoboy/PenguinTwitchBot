namespace DotNetTwitchBot.Bot.Models.Queues
{
    public class ActionExecutionLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ActionName { get; set; } = null!;
        public ActionExecutionState State { get; set; }
        public Dictionary<string, string> VariablesBefore { get; set; } = [];
        public Dictionary<string, string>? VariablesAfter { get; set; }
        public string QueueName { get; set; } = null!;
        public DateTime EnqueuedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }

        public TimeSpan? ExecutionDuration => CompletedAt.HasValue && StartedAt.HasValue 
            ? CompletedAt.Value - StartedAt.Value 
            : null;

        public TimeSpan? WaitTime => StartedAt.HasValue 
            ? StartedAt.Value - EnqueuedAt 
            : null;

        public TimeSpan? TotalTime => CompletedAt.HasValue 
            ? CompletedAt.Value - EnqueuedAt 
            : null;
    }
}
