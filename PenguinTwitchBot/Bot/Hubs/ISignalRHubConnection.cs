namespace PenguinTwitchBot.Bot.Hubs
{
    public interface ISignalRHubConnection : IAsyncDisposable
    {
        void On<T>(string eventName, Func<T, Task> handler);
        Task StartAsync();
    }
}
