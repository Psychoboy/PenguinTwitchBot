using PenguinTwitchBot.TwitchApi.Models.Games;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class GamesClient(ILogger<GamesClient> logger, IGamesTransport transport) : TwitchClientRetryBase(logger), IGamesClient
{
    public Task<GetGamesResponse> GetGamesAsync(string clientId, string? accessToken, List<string> gameIds)
    {
        return ExecuteWithRetryAsync(() => transport.GetGamesAsync(clientId, accessToken, gameIds), "fetch games");
    }
}
