using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IStreamsClient
{
    Task<GetStreamsResponse> GetStreamsAsync(string clientId, string? accessToken, List<string>? userIds);
}
