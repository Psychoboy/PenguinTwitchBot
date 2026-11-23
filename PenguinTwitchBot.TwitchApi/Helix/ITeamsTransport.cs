using PenguinTwitchBot.TwitchApi.Models.Teams;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface ITeamsTransport
{
    Task<GetChannelTeamsResponse> GetChannelTeamsAsync(string clientId, string? accessToken, string broadcasterId);
    Task<GetTeamsResponse> GetTeamByIdAsync(string clientId, string? accessToken, string teamId);
}
