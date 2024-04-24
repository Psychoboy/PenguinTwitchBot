namespace DotNetTwitchBot.Bot.StreamSchedule
{
    public class ScheduledStream
    {
        public string Title { get; set; } = "";
        public DateTime Start { get; set; } = DateTime.Now;
        public DateTime End { get; set; } = DateTime.Now;

    }
}
