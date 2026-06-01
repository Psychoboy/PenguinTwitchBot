using TwitchLib.Api.Helix.Models.Games;
using TwitchLibGame = TwitchLib.Api.Helix.Models.Games.Game;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class GamesClient(ILogger<GamesClient> logger, IGamesTransport transport) : TwitchClientRetryBase(logger), IGamesClient
{
    public Task<GetGamesResponse> GetGamesAsync(string clientId, string? accessToken, List<string> gameIds)
    {
        return ExecuteWithRetryAsync(() => transport.GetGamesAsync(clientId, accessToken, gameIds), "fetch games");
    }

    /// <summary>
    /// Maps a TwitchLib Game to the internal domain model
    /// </summary>
    public static Models.Games.Game MapToGame(TwitchLibGame source)
    {
        return new Models.Games.Game(
            Id: source.Id,
            Name: source.Name,
            BoxArtUrl: source.BoxArtUrl);
    }
}
