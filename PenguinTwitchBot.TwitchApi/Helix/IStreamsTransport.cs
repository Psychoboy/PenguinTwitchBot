using PenguinTwitchBot.TwitchApi.Models.Streams;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IStreamsTransport
{
    Task<GetStreamsResponse> GetStreamsAsync(string clientId, string? accessToken, List<string>? userIds);
}
