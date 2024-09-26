namespace DotNetTwitchBot.Bot.Models.Metrics
{
    public class SongRequestHistoryWithRank
    {
        public string SongId { get; set; } = null!;
        public string Title { get; set; } = null!;
        public int RequestedCount { get; set; }
        public int Ranking {  get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime LastRequestDate { get; set; }
    }
}
