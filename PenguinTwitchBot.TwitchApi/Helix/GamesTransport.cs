using System.Text.Json.Serialization;
using PenguinTwitchBot.TwitchApi.Models.Games;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class GamesTransport : IGamesTransport
{
    public async Task<GetGamesResponse> GetGamesAsync(string clientId, string? accessToken, List<string> gameIds)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = BuildGamesUrl(gameIds);
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetGamesApiResponse>(response);
        var games = payload?.Data.Select(MapToGame).ToList() ?? [];
        return new GetGamesResponse(games);
    }

    private static string BuildGamesUrl(List<string> gameIds)
    {
        var queryParts = new List<string>();
        foreach (var id in gameIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            queryParts.Add($"id={Uri.EscapeDataString(id)}");
        }

        return queryParts.Count == 0
            ? "games"
            : $"games?{string.Join("&", queryParts)}";
    }

    private static Game MapToGame(GameApiItem source)
    {
        return new Game(
            Id: source.Id,
            Name: source.Name,
            BoxArtUrl: source.BoxArtUrl);
    }

    private sealed record GetGamesApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<GameApiItem> Data);

    private sealed record GameApiItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("box_art_url")] string BoxArtUrl);
}
