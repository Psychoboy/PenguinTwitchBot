using System.ComponentModel.DataAnnotations;

namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class FishingSettings
    {
        [Key]
        public int Id { get; set; }
        public bool Enabled { get; set; } = true;
        public int DisplayDurationMs { get; set; } = 5000;
        public bool BoostMode { get; set; } = false;
        public double BoostModeRarityMultiplier { get; set; } = 2.0;
    }
}
