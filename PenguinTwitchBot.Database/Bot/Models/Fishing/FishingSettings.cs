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
        public Guid Id { get; set; }
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

        // Overlay animation & audio settings
        public bool EnableCastAnimation { get; set; } = true;
        public int CastAnimationMs { get; set; } = 1600;
        public bool SoundEnabled { get; set; } = true;
        public double SoundVolume { get; set; } = 0.8;

        // Optional global SFX override URLs/paths (empty = bundled default MP3; per-fish files still take top priority)
        public string? CastSoundPath { get; set; }
        public string? BiteSoundPath { get; set; }
        public string? CatchCommonSoundPath { get; set; }
        public string? CatchRareSoundPath { get; set; }
        public string? CatchLegendarySoundPath { get; set; }
        public string? AccidentSoundPath { get; set; }
        public string? FrenzySoundPath { get; set; }

        // Built-in multi-point catch rewards & Quick Tournament defaults (JSON-serialized)
        public string? CatchRewardsJson { get; set; }
        public string? QuickTournamentRewardsJson { get; set; }

        // Tournament chat hype settings
        public bool AnnounceTournamentLeadChanges { get; set; } = true;
        public bool AnnounceTournamentCountdown { get; set; } = true;

        /// <summary>Returns a detached shallow copy so callers (e.g. admin editors) cannot mutate cached settings before saving.</summary>
        public FishingSettings Clone() => (FishingSettings)MemberwiseClone();
    }
}
