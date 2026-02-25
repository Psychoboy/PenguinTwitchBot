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
        public int IntervalMinimum { get; set; } = 5;
        public int IntervalMaximum { get; set; } = 15;
        public int MinimumMessages { get; set; } = 15;
        [Required]
        public bool Shuffle { get; set; } = true;
        public DateTime LastRun { get; set; }
        public DateTime NextRun { get; set; }

        public List<PlatformType> Platforms { get; set; } = new List<PlatformType>() { PlatformType.Twitch };

        public List<TimerMessage> Messages { get; set; } = new List<TimerMessage>();
    }
}