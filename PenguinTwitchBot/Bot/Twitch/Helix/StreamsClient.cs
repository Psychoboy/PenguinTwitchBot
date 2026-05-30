using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class StreamsClient(ILogger<StreamsClient> logger, IStreamsTransport transport) : TwitchClientRetryBase(logger), IStreamsClient
{
    public Task<GetStreamsResponse> GetStreamsAsync(string clientId, string? accessToken, List<string>? userIds)
    {
        return ExecuteWithRetryAsync(() => transport.GetStreamsAsync(clientId, accessToken, userIds), "fetch streams");
    }
}
