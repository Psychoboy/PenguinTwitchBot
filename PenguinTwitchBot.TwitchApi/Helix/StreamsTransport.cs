using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class StreamsTransport : IStreamsTransport
{
    public Task<GetStreamsResponse> GetStreamsAsync(string clientId, string? accessToken, List<string>? userIds)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Streams.GetStreamsAsync(userIds: userIds, first: 100, accessToken: accessToken);
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
