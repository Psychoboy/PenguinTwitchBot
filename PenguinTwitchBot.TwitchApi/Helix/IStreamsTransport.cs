using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IStreamsTransport
{
    Task<GetStreamsResponse> GetStreamsAsync(string clientId, string? accessToken, List<string>? userIds);
}
