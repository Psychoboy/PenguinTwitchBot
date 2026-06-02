using PenguinTwitchBot.TwitchApi.Models.Schedule;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ScheduleClient(ILogger<ScheduleClient> logger, IScheduleTransport transport) : TwitchClientRetryBase(logger), IScheduleClient
{
    public Task<GetChannelStreamScheduleResponse> GetChannelStreamScheduleAsync(string clientId, string? accessToken, string broadcasterId)
    {
        return ExecuteWithRetryAsync(() => transport.GetChannelStreamScheduleAsync(clientId, accessToken, broadcasterId), "get channel stream schedule");
    }
}
