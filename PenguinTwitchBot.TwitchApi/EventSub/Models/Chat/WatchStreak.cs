namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class WatchStreak
{
    public int StreakCount { get; set; }
    public int ChannelPointsAwarded { get; set; }
}