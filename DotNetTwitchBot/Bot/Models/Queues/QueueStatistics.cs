namespace DotNetTwitchBot.Bot.Models.Queues
{
    public class QueueStatistics
    {
        public string QueueName { get; set; } = null!;
        public int PendingActions { get; set; }
        public long CompletedActions { get; set; }
        public bool IsBlocking { get; set; }
        public bool IsEnabled { get; set; }
        public int MaxConcurrentActions { get; set; }
        public int CurrentlyExecuting { get; set; }
    }
}
