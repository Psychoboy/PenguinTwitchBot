namespace DotNetTwitchBot.Bot.Models.Metrics
{
    public class SongRequestHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SongId { get; set; } = null!;
        public string Title { get; set; } = null!;
        public TimeSpan Duration { get; set; }
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    }
}
