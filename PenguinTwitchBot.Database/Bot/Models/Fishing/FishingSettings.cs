using System.ComponentModel.DataAnnotations;

namespace PenguinTwitchBot.Database.Bot.Models.Fishing
{
    public class FishingSettings
    {
        public const double DefaultLineSnapChance = 0.02;
        public const double DefaultRodSnapChance = 0.0005;
        public const double DefaultReelJamChance = 0.0;
        public const double DefaultTackleBoxLostChance = 0.0;
        public const double DefaultNetBreakChance = 0.0;
        public const double DefaultRepairCostMultiplier = 0.0;

        [Key]
        public int Id { get; set; }
        public bool Enabled { get; set; } = true;
        public int DisplayDurationMs { get; set; } = 5000;
        public bool BoostMode { get; set; } = false;
        public double BoostModeRarityMultiplier { get; set; } = 2.0;
        public double LineSnapChance { get; set; } = DefaultLineSnapChance;
        public double RodSnapChance { get; set; } = DefaultRodSnapChance;
        public double ReelJamChance { get; set; } = DefaultReelJamChance;
        public double TackleBoxLostChance { get; set; } = DefaultTackleBoxLostChance;
        public double NetBreakChance { get; set; } = DefaultNetBreakChance;
        public double RepairCostMultiplier { get; set; } = DefaultRepairCostMultiplier;

        // Rarity thresholds based on BaseGold values
        public int RarityUncommonThreshold { get; set; } = 35;
        public int RarityRareThreshold { get; set; } = 60;
        public int RarityEpicThreshold { get; set; } = 110;
        public int RarityLegendaryThreshold { get; set; } = 201;
        public int RarityMythicalThreshold { get; set; } = 300;
    }
}
