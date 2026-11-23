using System.Text.Json.Serialization;
using PenguinTwitchBot.TwitchApi.Models.Teams;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class TeamsTransport : ITeamsTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public TeamsTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GetChannelTeamsResponse> GetChannelTeamsAsync(string clientId, string? accessToken, string broadcasterId)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = HelixQuery.Build("teams/channel", new (string Key, string? Value)[]
        {
            ("broadcaster_id", broadcasterId)
        });
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetChannelTeamsApiResponse>(response);
        var teams = payload?.Data?.Select(MapToChannelTeam).ToList() ?? [];
        return new GetChannelTeamsResponse(teams);
    }

    public async Task<GetTeamsResponse> GetTeamByIdAsync(string clientId, string? accessToken, string teamId)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = HelixQuery.Build("teams", new (string Key, string? Value)[]
        {
            ("id", teamId)
        });
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetTeamsApiResponse>(response);
        var teams = payload?.Data?.Select(MapToTeam).ToList() ?? [];
        return new GetTeamsResponse(teams);
    }

    private static ChannelTeam MapToChannelTeam(ChannelTeamApiItem source)
    {
        return new ChannelTeam(
            Id: source.Id,
            TeamName: source.TeamName,
            TeamDisplayName: source.TeamDisplayName,
            BroadcasterId: source.BroadcasterId,
            BroadcasterLogin: source.BroadcasterLogin,
            BroadcasterName: source.BroadcasterName);
    }

    private static Team MapToTeam(TeamApiItem source)
    {
        var users = source.Users?.Select(u => new TeamMember(
            UserId: u.UserId,
            UserLogin: u.UserLogin,
            UserName: u.UserName)).ToList() ?? [];

        return new Team(
            Id: source.Id,
            TeamName: source.TeamName,
            TeamDisplayName: source.TeamDisplayName,
            Users: users);
    }

    private sealed record GetChannelTeamsApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<ChannelTeamApiItem>? Data);

    private sealed record ChannelTeamApiItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("team_name")] string TeamName,
        [property: JsonPropertyName("team_display_name")] string TeamDisplayName,
        [property: JsonPropertyName("broadcaster_id")] string BroadcasterId,
        [property: JsonPropertyName("broadcaster_login")] string BroadcasterLogin,
        [property: JsonPropertyName("broadcaster_name")] string BroadcasterName);

    private sealed record GetTeamsApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<TeamApiItem>? Data);

    private sealed record TeamApiItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("team_name")] string TeamName,
        [property: JsonPropertyName("team_display_name")] string TeamDisplayName,
        [property: JsonPropertyName("users")] IReadOnlyList<TeamMemberApiItem>? Users);

    private sealed record TeamMemberApiItem(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("user_login")] string UserLogin,
        [property: JsonPropertyName("user_name")] string UserName);
}
