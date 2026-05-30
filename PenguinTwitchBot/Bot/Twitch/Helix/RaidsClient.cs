namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class RaidsClient(ILogger<RaidsClient> logger, IRaidsTransport transport) : TwitchClientRetryBase(logger), IRaidsClient
{
    public Task StartRaidAsync(string clientId, string? accessToken, string broadcasterId, string targetUserId)
    {
        return ExecuteWithRetryAsync(() => transport.StartRaidAsync(clientId, accessToken, broadcasterId, targetUserId), "start raid");
    }
}
