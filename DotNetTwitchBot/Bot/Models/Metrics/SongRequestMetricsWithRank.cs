namespace DotNetTwitchBot.Bot.Models.Metrics
{
    public class SongRequestMetricsWithRank
    {
        public string SongId { get; set; } = null!;
        public string Title { get; set; } = null!;
        public TimeSpan Duration { get; set; }
        public int RequestedCount { get; set; }
        public int Ranking { get; set; }
    }
}