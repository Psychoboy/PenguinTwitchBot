namespace PenguinTwitchBot.Bot.Hubs;

public static class HubNames
{
    public const string MainHub = nameof(Bot.Hubs.MainHub);
    public const string YtHub = nameof(Bot.Commands.Music.YtHub);
}

public interface IInProcessHubEventBus
{
    IDisposable Subscribe(string hubName, string methodName, Func<object?[], Task> handler);
    Task PublishAsync(string hubName, string methodName, object?[] args, CancellationToken cancellationToken = default);
}
