using TwitchLib.Api.Helix.Models.Schedule.GetChannelStreamSchedule;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public interface IScheduleClient
{
    Task<GetChannelStreamScheduleResponse> GetChannelStreamScheduleAsync(string clientId, string? accessToken, string broadcasterId);
}
