using DotNetTwitchBot.Bot.Models.Fishing;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingHelpDataService
    {
        /// <summary>
        /// Get comprehensive help data for the fishing help page, computed from current shop/settings.
        /// Data is cached indefinitely; call InvalidateCache() after admin changes.
        /// </summary>
        Task<FishingHelpData> GetHelpDataAsync();

        /// <summary>
        /// Invalidate the cached help data. Call this after admin changes to shop items, settings, or fish types.
        /// </summary>
        void InvalidateCache();
    }

    /// <summary>
    /// DTO containing all data for the fishing help page.
    /// </summary>
    public class FishingHelpData
    {
        public RarityGuideSection RarityGuide { get; set; } = new();
        public StarWeightSection StarWeight { get; set; } = new();
        public GoldSection Gold { get; set; } = new();
        public EquipmentMetaSection EquipmentMeta { get; set; } = new();
        public ConsumableRosterSection ConsumableRoster { get; set; } = new();
        public MetaBuildsSection MetaBuilds { get; set; } = new();
    }

    public class RarityGuideSection
    {
        /// <summary>
        /// Baseline rarity percentages (baseline without boosts/equipment).
        /// </summary>
        public Dictionary<FishRarity, double> BaselinePercentages { get; set; } = new();

        /// <summary>
        /// Is boost mode enabled? If true, rare+ rarities are multiplied.
        /// </summary>
        public bool BoostModeEnabled { get; set; }

        /// <summary>
        /// Boost mode multiplier (if enabled).
        /// </summary>
        public double? BoostModeMultiplier { get; set; }

        /// <summary>
        /// Examples of rarity-specific equipment.
        /// </summary>
        public List<HelpItemExample> RarityBoostExamples { get; set; } = new();

        /// <summary>
        /// Description of how equipment stacks and how boost mode works.
        /// </summary>
        public string EffectsNoteHtml { get; set; } = string.Empty;
    }

    public class StarWeightSection
    {
        /// <summary>
        /// Star distribution baseline (percentages without boosts).
        /// </summary>
        public Dictionary<int, double> StarDistribution { get; set; } = new();

        /// <summary>
        /// Impact per star on weight (multiplier).
        /// </summary>
        public Dictionary<int, double> WeightMultiplierPerStar { get; set; } = new();

        /// <summary>
        /// Impact per star on gold (multiplier).
        /// </summary>
        public Dictionary<int, double> GoldMultiplierPerStar { get; set; } = new();

        /// <summary>
        /// Examples of star-boosting equipment.
        /// </summary>
        public List<HelpItemExample> StarBoostExamples { get; set; } = new();
    }

    public class GoldSection
    {
        /// <summary>
        /// Gold value per star (baseline range without boosts).
        /// </summary>
        public Dictionary<int, string> GoldRangePerStar { get; set; } = new(); // "3-star: 1250-1410g"

        /// <summary>
        /// Weight multiplier impact on final gold.
        /// </summary>
        public string WeightMultiplierNote { get; set; } = string.Empty;

        /// <summary>
        /// Examples of high-value catches.
        /// </summary>
        public List<string> ValueExamples { get; set; } = new();
    }

    public class EquipmentMetaSection
    {
        /// <summary>
        /// Recommended equipment for all-around fishing (balanced stats).
        /// </summary>
        public List<HelpItemExample> AllRoundBuild { get; set; } = new();

        /// <summary>
        /// Recommended equipment for rarity hunting.
        /// </summary>
        public List<HelpItemExample> RarityHunterBuild { get; set; } = new();

        /// <summary>
        /// Recommended equipment for star/weight maximization.
        /// </summary>
        public List<HelpItemExample> HighValueBuild { get; set; } = new();

        /// <summary>
        /// Slot descriptions and what to equip.
        /// </summary>
        public Dictionary<string, string> SlotDescriptions { get; set; } = new();
    }

    public class ConsumableRosterSection
    {
        /// <summary>
        /// All consumable (single-use or limited-use) items in the shop.
        /// </summary>
        public List<HelpItemExample> ConsumableItems { get; set; } = new();

        /// <summary>
        /// Note about how consumables work (uses, targeting, etc).
        /// </summary>
        public string ConsumableNote { get; set; } = string.Empty;
    }

    public class MetaBuildsSection
    {
        /// <summary>
        /// Suggested builds with descriptions of playstyle and expected outcomes.
        /// </summary>
        public List<BuildDescription> Builds { get; set; } = new();
    }

    /// <summary>
    /// Compact example of a shop item for help page display.
    /// </summary>
    public class HelpItemExample
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Cost { get; set; }
        public string Boosts { get; set; } = string.Empty; // e.g., "+50% rarity, +1.2x weight"
        public string TargetFish { get; set; } = string.Empty; // if specific-fish boost
        public int MaxUses { get; set; } // 0 = unlimited/permanent
    }

    /// <summary>
    /// Description of a suggested playstyle/build.
    /// </summary>
    public class BuildDescription
    {
        public string Name { get; set; } = string.Empty;
        public string PlayStyle { get; set; } = string.Empty;
        public string ExpectedOutcome { get; set; } = string.Empty;
        public int ApproximateCostToSetUp { get; set; }
    }
}
