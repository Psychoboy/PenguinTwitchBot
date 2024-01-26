namespace DotNetTwitchBot.Bot.Events
{
    public class AdBreakStartEventArgs
    {
        public int Length { get; set; }
        public bool Automatic { get; set; }
        public DateTimeOffset StartedAt { get; set; }
    }
}
