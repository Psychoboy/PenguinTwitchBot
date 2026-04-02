using DotNetTwitchBot.Bot.Actions;

namespace DotNetTwitchBot.Bot.Queues
{
    public interface IActionQueue
    {
        string Name { get; }
        bool IsBlocking { get; }
        bool IsEnabled { get; set; }
        int MaxConcurrentActions { get; }
        int PendingCount { get; }
        long CompletedCount { get; }
        int CurrentlyExecuting { get; }
        
        Task EnqueueAsync(ActionType action, Dictionary<string, string> variables);
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync();
    }
}
