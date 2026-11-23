using PenguinTwitchBot.TwitchApi.Models.Teams;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class TeamsClient(ILogger<TeamsClient> logger, ITeamsTransport transport) : TwitchClientRetryBase(logger), ITeamsClient
{
    public Task<GetChannelTeamsResponse> GetChannelTeamsAsync(string clientId, string? accessToken, string broadcasterId)
    {
        return ExecuteWithRetryAsync(() => transport.GetChannelTeamsAsync(clientId, accessToken, broadcasterId), "fetch channel teams");
    }

    public Task<GetTeamsResponse> GetTeamByIdAsync(string clientId, string? accessToken, string teamId)
    {
        return ExecuteWithRetryAsync(() => transport.GetTeamByIdAsync(clientId, accessToken, teamId), "fetch team details");
    }
}
