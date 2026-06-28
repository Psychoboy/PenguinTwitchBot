namespace PenguinTwitchBot.Bot.Hubs
{
    public interface ISignalRHubConnection : IAsyncDisposable
    {
        IDisposable On<T>(string eventName, Func<T, Task> handler);
        Task StartAsync();
    }
}
