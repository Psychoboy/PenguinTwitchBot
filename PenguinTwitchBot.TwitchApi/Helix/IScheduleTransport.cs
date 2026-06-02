using PenguinTwitchBot.TwitchApi.Models.Schedule;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IScheduleTransport
{
    Task<GetChannelStreamScheduleResponse> GetChannelStreamScheduleAsync(string clientId, string? accessToken, string broadcasterId);
}
