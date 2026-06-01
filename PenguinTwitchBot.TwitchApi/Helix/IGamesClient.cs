using TwitchLib.Api.Helix.Models.Games;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IGamesClient
{
    Task<GetGamesResponse> GetGamesAsync(string clientId, string? accessToken, List<string> gameIds);
}
