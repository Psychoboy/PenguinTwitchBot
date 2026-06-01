using TwitchLib.Api;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class RaidsTransport : IRaidsTransport
{
    public Task StartRaidAsync(string clientId, string? accessToken, string broadcasterId, string targetUserId)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Raids.StartRaidAsync(broadcasterId, targetUserId, accessToken);
    }

    private static TwitchAPI CreateApi(string clientId, string? accessToken)
    {
        var api = new TwitchAPI();
        api.Settings.ClientId = clientId;
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            api.Settings.AccessToken = accessToken;
        }

        return api;
    }
}
