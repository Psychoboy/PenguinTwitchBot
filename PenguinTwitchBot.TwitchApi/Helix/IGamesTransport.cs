using TwitchLib.Api.Helix.Models.Games;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public interface IGamesTransport
{
    Task<GetGamesResponse> GetGamesAsync(string clientId, string? accessToken, List<string> gameIds);
}
