using PenguinTwitchBot.TwitchApi.Models.Streams;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IStreamsClient
{
    Task<GetStreamsResponse> GetStreamsAsync(string clientId, string? accessToken, List<string>? userIds);
}
