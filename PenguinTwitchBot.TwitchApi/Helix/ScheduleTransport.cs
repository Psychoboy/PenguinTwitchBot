using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Schedule.GetChannelStreamSchedule;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ScheduleTransport : IScheduleTransport
{
    public Task<GetChannelStreamScheduleResponse> GetChannelStreamScheduleAsync(string clientId, string? accessToken, string broadcasterId)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Schedule.GetChannelStreamScheduleAsync(broadcasterId);
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
