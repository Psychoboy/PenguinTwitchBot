using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Fishing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    /// <summary>
    /// Generates dynamic help content from current shop items, settings, and fish types.
    /// Data is cached indefinitely; call InvalidateCache() after admin changes.
    /// </summary>
    public class FishingHelpDataService : IFishingHelpDataService
    {
        private const string CacheKey = "FishingHelpData";
        private readonly IMemoryCache _cache;
        private readonly ILogger<FishingHelpDataService> _logger;
        private readonly IFishingService _fishingService;
        private readonly IFishingShopService _shopService;

        public FishingHelpDataService(
            IServiceScopeFactory scopeFactory,
            IMemoryCache cache,
            ILogger<FishingHelpDataService> logger,
            IFishingService fishingService,
            IFishingShopService shopService)
        {
            _cache = cache;
            _logger = logger;
            _fishingService = fishingService;
            _shopService = shopService;
        }

        public async Task<FishingHelpData> GetHelpDataAsync()
        {
            if (_cache.TryGetValue(CacheKey, out FishingHelpData? cachedData) && cachedData != null)
            {
                _logger.LogDebug("Returning cached fishing help data");
                return cachedData;
            }

            _logger.LogInformation("Computing fishing help data (not in cache)");

            var data = await ComputeHelpDataAsync();
            _cache.Set(CacheKey, data);
            return data;
        }

        public void InvalidateCache()
        {
            _logger.LogInformation("Invalidating fishing help data cache");
            _cache.Remove(CacheKey);
        }

        private async Task<FishingHelpData> ComputeHelpDataAsync()
        {
            var data = new FishingHelpData();

            // Get current shop items, fish types, and settings
            var shopItems = await _shopService.GetAllShopItems();
            var fishTypes = await _fishingService.GetAllFishTypes();
            var settings = await _fishingService.GetSettings();

            // Build each section
            data.RarityGuide = BuildRarityGuideSection(shopItems, settings);
            data.StarWeight = BuildStarWeightSection(shopItems);
            data.Gold = BuildGoldSection();
            data.EquipmentMeta = BuildEquipmentMetaSection(shopItems, fishTypes);
            data.ConsumableRoster = BuildConsumableRosterSection(shopItems);
            data.MetaBuilds = BuildMetaBuildsSection(shopItems);

            return data;
        }

        private RarityGuideSection BuildRarityGuideSection(List<FishingShopItem> shopItems, FishingSettings? settings)
        {
            var section = new RarityGuideSection
            {
                BaselinePercentages = new Dictionary<FishRarity, double>
                {
                    { FishRarity.Common, 50.0 },
                    { FishRarity.Uncommon, 30.0 },
                    { FishRarity.Rare, 15.0 },
                    { FishRarity.Epic, 4.0 },
                    { FishRarity.Legendary, 1.0 }
                },
                BoostModeEnabled = settings?.BoostMode ?? false,
                BoostModeMultiplier = settings?.BoostModeRarityMultiplier
            };

            // Find rarity boost examples
            var rarityBoosts = shopItems
                .Where(s => s.Enabled && (s.BoostType == FishingBoostType.GeneralRarityBoost
                    || s.BoostType2 == FishingBoostType.GeneralRarityBoost
                    || s.BoostType3 == FishingBoostType.GeneralRarityBoost))
                .OrderBy(s => s.Cost)
                .Take(5)
                .ToList();

            section.RarityBoostExamples = rarityBoosts.Select(CreateItemExample).ToList();

            var boostText = settings?.BoostMode == true
                ? $"Boost Mode is <strong>enabled</strong> with {settings.BoostModeRarityMultiplier}x rarity multiplier on Uncommon+. "
                : "Boost Mode is <strong>disabled</strong>. ";

            section.EffectsNoteHtml = boostText + 
                "Rarity and weight equipment boosts stack multiplicatively (each compounds the last). Star boosts stack additively. " +
                "Specific-fish boosts work independently and can exceed the shop item cap.";

            return section;
        }

        private StarWeightSection BuildStarWeightSection(List<FishingShopItem> shopItems)
        {
            var section = new StarWeightSection
            {
                StarDistribution = new Dictionary<int, double>
                {
                    { 3, 5.0 }, 
                    { 2, 20.0 }, 
                    { 1, 75.0 }  
                },
                WeightMultiplierPerStar = new Dictionary<int, double>
                {
                    { 3, 1.5 }, 
                    { 2, 1.2 }, 
                    { 1, 1.0 }  
                },
                GoldMultiplierPerStar = new Dictionary<int, double>
                {
                    { 3, 1.5 },
                    { 2, 1.2 },
                    { 1, 1.0 }
                }
            };

            // Find star boost examples
            var starBoosts = shopItems
                .Where(s => s.Enabled && (s.BoostType == FishingBoostType.StarBoost
                    || s.BoostType2 == FishingBoostType.StarBoost
                    || s.BoostType3 == FishingBoostType.StarBoost))
                .OrderBy(s => s.Cost)
                .Take(5)
                .ToList();

            section.StarBoostExamples = starBoosts.Select(CreateItemExample).ToList();

            return section;
        }

        private GoldSection BuildGoldSection()
        {
            var section = new GoldSection();
            
            section.GoldRangePerStar = new Dictionary<int, string>
            {
                { 3, "1.25–1.41x gold multiplier (3-star)" },
                { 2, "1.0–1.25x gold multiplier (2-star)" },
                { 1, "0.75–1.0x gold multiplier (1-star)" }
            };

            section.WeightMultiplierNote = "Final gold = fish base gold × star multiplier × weight multiplier. " +
                "Weight ranges from 0.9x to 1.065x of base weight, providing -10% to +6.5% gold bonus. " +
                "Example: A 100 base gold fish with 3-star (1.41x max) and heavy weight (1.065x) yields ~150g.";

            section.ValueExamples = new List<string>
            {
                "3-star at max weight: base gold × 1.41 × 1.065 = +50% total",
                "2-star at average: base gold × 1.125 × 1.0 = +12.5% total",
                "1-star at light: base gold × 0.875 × 0.9 = -21.25% total"
            };

            return section;
        }

        private EquipmentMetaSection BuildEquipmentMetaSection(List<FishingShopItem> shopItems, List<FishType> fishTypes)
        {
            var section = new EquipmentMetaSection();

            // Equipment slot descriptions
            section.SlotDescriptions = new Dictionary<string, string>
            {
                { "Rod", "Main fishing rod — provides general rarity boost" },
                { "Reel", "Fishing reel — provides star boost" },
                { "Line", "Fishing line — provides weight boost" },
                { "Hook", "Fish hook — provides star boost" },
                { "Bait", "Consumable bait — targets specific fish types" },
                { "Lure", "Consumable lure — targets specific fish + boosts rarity" },
                { "TackleBox", "Permanent accessory — multiple small boosts" },
                { "Net", "Permanent accessory — weight bonus" },
                { "Special", "Reserved for special event items" }
            };

            // All-around build: balanced rarity + weight + star
            var enabledItems = shopItems.Where(s => s.Enabled && !s.IsConsumable).OrderBy(s => s.Cost).ToList();
            section.AllRoundBuild = enabledItems.Take(3).Select(CreateItemExample).ToList();

            // Rarity hunter build: focus on rarity boosts
            var rarityItems = enabledItems
                .Where(s => s.BoostType == FishingBoostType.GeneralRarityBoost 
                    || s.BoostType2 == FishingBoostType.GeneralRarityBoost
                    || s.BoostType3 == FishingBoostType.GeneralRarityBoost)
                .OrderBy(s => s.Cost)
                .Take(3)
                .ToList();
            section.RarityHunterBuild = rarityItems.Select(CreateItemExample).ToList();

            // High-value build: focus on weight + star
            var valueItems = enabledItems
                .Where(s => s.BoostType == FishingBoostType.WeightBoost || s.BoostType == FishingBoostType.StarBoost
                    || s.BoostType2 == FishingBoostType.WeightBoost || s.BoostType2 == FishingBoostType.StarBoost
                    || s.BoostType3 == FishingBoostType.WeightBoost || s.BoostType3 == FishingBoostType.StarBoost)
                .OrderBy(s => s.Cost)
                .Take(3)
                .ToList();
            section.HighValueBuild = valueItems.Select(CreateItemExample).ToList();

            return section;
        }

        private ConsumableRosterSection BuildConsumableRosterSection(List<FishingShopItem> shopItems)
        {
            var section = new ConsumableRosterSection();

            var consumables = shopItems
                .Where(s => s.Enabled && s.IsConsumable)
                .OrderBy(s => s.Cost)
                .ToList();

            section.ConsumableItems = consumables.Select(CreateItemExample).ToList();

            section.ConsumableNote = "Consumable items grant temporary boosts and are removed after their uses expire. " +
                "Use them strategically to target specific fish or maximize catch value during peak times.";

            return section;
        }

        private MetaBuildsSection BuildMetaBuildsSection(List<FishingShopItem> shopItems)
        {
            var section = new MetaBuildsSection
            {
                Builds = new List<BuildDescription>
                {
                    new BuildDescription
                    {
                        Name = "Beginner's Luck",
                        PlayStyle = "Early-game grind with minimal investment.",
                        ExpectedOutcome = "Steady gold income, modest catch variety.",
                        ApproximateCostToSetUp = 250
                    },
                    new BuildDescription
                    {
                        Name = "Rarity Hunter",
                        PlayStyle = "Focus on landing rare and epic fish; sacrifice weight/gold per fish for frequency of high-tier catches.",
                        ExpectedOutcome = "High catch variety, satisfying trophy moments.",
                        ApproximateCostToSetUp = 800
                    },
                    new BuildDescription
                    {
                        Name = "High-Value Farmer",
                        PlayStyle = "Maximize gold-per-catch by stacking weight and star boosts.",
                        ExpectedOutcome = "Lower catch frequency, higher gold per fish.",
                        ApproximateCostToSetUp = 1200
                    },
                    new BuildDescription
                    {
                        Name = "Legendary Collector",
                        PlayStyle = "Combine rarity boosts with high-value multipliers; use specific-fish consumables to guarantee legendary hunts.",
                        ExpectedOutcome = "Rare legendary catches with maximum value.",
                        ApproximateCostToSetUp = 2500
                    }
                }
            };

            return section;
        }

        private HelpItemExample CreateItemExample(FishingShopItem item)
        {
            var boosts = new List<string>();

            if (item.BoostAmount > 0)
            {
                boosts.Add(FormatBoost(item.BoostType, item.BoostAmount));
            }

            if (item.BoostType2.HasValue && item.BoostAmount2 > 0)
            {
                boosts.Add(FormatBoost(item.BoostType2.Value, item.BoostAmount2.Value));
            }

            if (item.BoostType3.HasValue && item.BoostAmount3 > 0)
            {
                boosts.Add(FormatBoost(item.BoostType3.Value, item.BoostAmount3.Value));
            }

            var example = new HelpItemExample
            {
                Name = item.Name,
                Description = item.Description,
                Cost = item.Cost,
                Boosts = string.Join(", ", boosts),
                TargetFish = item.TargetFishType?.Name ?? string.Empty,
                MaxUses = item.MaxUses ?? 0
            };

            return example;
        }

        private string FormatBoost(FishingBoostType type, double amount)
        {
            return type switch
            {
                FishingBoostType.GeneralRarityBoost => $"+{(amount * 100):F0}% rarity",
                FishingBoostType.SpecificFishBoost => $"+{(amount * 100):F0}% specific fish",
                FishingBoostType.WeightBoost => $"+{(amount * 100):F0}% weight",
                FishingBoostType.StarBoost => $"+{(amount * 100):F0}% stars",
                _ => string.Empty
            };
        }
    }
}
