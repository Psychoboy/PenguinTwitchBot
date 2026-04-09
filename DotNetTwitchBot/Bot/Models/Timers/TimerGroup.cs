using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Timers
{
    public class TimerGroup
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string Name { get; set; } = null!;
        public bool Active { get; set; } = true;
        public bool Repeat { get; set; } = true;
        public bool OnlineOnly { get; set; } = true;
        public int IntervalMinimumSeconds { get; set; } = 300;
        public int IntervalMaximumSeconds { get; set; } = 900;
        public int MinimumMessages { get; set; } = 15;
        [Required]
        public bool Shuffle { get; set; } = true;
        public DateTime LastRun { get; set; }
        public DateTime NextRun { get; set; }
    }
}