using System.ComponentModel.DataAnnotations;

namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class FishingSettings
    {
        public const double DefaultLineSnapChance = 0.02;
        public const double DefaultRodSnapChance = 0.0005;

        [Key]
        public int Id { get; set; }
        public bool Enabled { get; set; } = true;
        public int DisplayDurationMs { get; set; } = 5000;
        public bool BoostMode { get; set; } = false;
        public double BoostModeRarityMultiplier { get; set; } = 2.0;
        public double LineSnapChance { get; set; } = DefaultLineSnapChance;
        public double RodSnapChance { get; set; } = DefaultRodSnapChance;

        // Rarity thresholds based on BaseGold values
        public int RarityUncommonThreshold { get; set; } = 35;
        public int RarityRareThreshold { get; set; } = 60;
        public int RarityEpicThreshold { get; set; } = 110;
        public int RarityLegendaryThreshold { get; set; } = 201;
    }
}
