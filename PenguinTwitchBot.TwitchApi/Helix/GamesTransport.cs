using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Games;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class GamesTransport : IGamesTransport
{
    public Task<GetGamesResponse> GetGamesAsync(string clientId, string? accessToken, List<string> gameIds)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Games.GetGamesAsync(gameIds);
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
