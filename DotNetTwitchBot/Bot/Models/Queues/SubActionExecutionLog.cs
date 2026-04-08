namespace DotNetTwitchBot.Bot.Models.Queues
{
    public class SubActionExecutionLog
    {
        public string SubActionType { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsSuccess { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public List<string> Messages { get; set; } = [];
        public int Depth { get; set; }

        public Guid? ChildActionLogId { get; set; }

        public TimeSpan? Duration => CompletedAt.HasValue 
            ? CompletedAt.Value - StartedAt 
            : null;
    }
}
