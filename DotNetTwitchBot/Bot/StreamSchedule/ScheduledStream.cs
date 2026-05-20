namespace DotNetTwitchBot.Bot.StreamSchedule
{
    public class ScheduledStream
    {
        public string Title { get; set; } = "";
        public DateTime Start { get; set; } = DateTime.UtcNow;
        public DateTime End { get; set; } = DateTime.UtcNow;
        public ulong DiscordEventId { get; set; } = 0;
        public string TwitchEventId { get; set; } = "";

    }
}
