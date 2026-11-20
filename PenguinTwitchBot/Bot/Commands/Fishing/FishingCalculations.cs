using PenguinTwitchBot.Database.Bot.Models.Fishing;
using System.Security.Cryptography;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    internal sealed class FishingSelectionResult
    {
        public required FishType Fish { get; init; }
        public List<FishingTriggeredBoostInfo> TriggeredBoosts { get; init; } = new();
        public bool WasFrenzyInfluenced { get; init; }
    }

    /// <summary>
    /// Shared calculation logic for fishing mechanics (rarity selection, stars, weight, gold)
    /// </summary>
    internal static class FishingCalculations
    {
        public static FishType SelectRandomFish(
            List<FishType> fishTypes,
            FishingSettings? settings,
            List<UserFishingBoost> boosts,
            FishingFrenzyState? activeFrenzy = null)
        {
            return SelectRandomFishWithDetails(fishTypes, settings, boosts, activeFrenzy).Fish;
        }

        public static FishingSelectionResult SelectRandomFishWithDetails(
            List<FishType> fishTypes,
            FishingSettings? settings,
            List<UserFishingBoost> boosts,
            FishingFrenzyState? activeFrenzy = null)
        {
            var triggeredBoosts = new List<FishingTriggeredBoostInfo>();
            var pool = fishTypes;
            var wasFrenzyInfluenced = activeFrenzy != null && (activeFrenzy.RarityMultiplier > 1.0 || activeFrenzy.CategoryBoost > 0);

            // 1. Rarity-neutral category attraction for SpecificCategoryBoost items (and Frenzy hotspot category)
            var categoryBoostItems = GetCategoryBoostEntries(boosts);
            string? attractedCategory = null;
            UserFishingBoost? attractingBoostItem = null;

            if (categoryBoostItems.Count > 0)
            {
                // Sum positive category boosts per category and roll attraction
                var byCategory = categoryBoostItems
                    .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new
                    {
                        Category = g.Key,
                        TotalBoost = g.Sum(x => x.Amount),
                        PrimaryBoost = g.OrderByDescending(x => x.Amount).First().Boost
                    })
                    .OrderByDescending(x => x.TotalBoost)
                    .ToList();

                foreach (var entry in byCategory)
                {
                    if (entry.TotalBoost <= 0) continue;
                    var matchingFish = fishTypes.Where(f => FishHasCategory(f, entry.Category)).ToList();
                    if (matchingFish.Count == 0) continue;

                    var attractChance = Math.Clamp(entry.TotalBoost, 0.0, 0.85);
                    if (NextUnitDouble() < attractChance)
                    {
                        pool = matchingFish;
                        attractedCategory = entry.Category;
                        attractingBoostItem = entry.PrimaryBoost;
                        break;
                    }
                }
            }

            // Frenzy hotspot category attraction if player didn't already lock a category via bait
            if (attractedCategory == null &&
                activeFrenzy != null &&
                !string.IsNullOrWhiteSpace(activeFrenzy.TargetCategory) &&
                activeFrenzy.CategoryBoost > 0)
            {
                var frenzyMatching = fishTypes.Where(f => FishHasCategory(f, activeFrenzy.TargetCategory)).ToList();
                if (frenzyMatching.Count > 0 && NextUnitDouble() < Math.Clamp(activeFrenzy.CategoryBoost, 0.0, 0.85))
                {
                    pool = frenzyMatching;
                    attractedCategory = activeFrenzy.TargetCategory;
                }
            }

            // 2. Roll fish using unified single-fish-safe weighting (supports Consumable GeneralRarityBoost Advantage)
            var consumableRarityBoost = GetConsumableGeneralRarityBoost(boosts, out var primaryConsumableRarityItem);
            var primaryFish = RollFishFromPool(pool, settings, boosts, activeFrenzy);
            var selectedFish = primaryFish;
            var advantageUpgraded = false;

            if (consumableRarityBoost >= 0.25)
            {
                var advantageChance = Math.Min(0.75, consumableRarityBoost);
                if (NextUnitDouble() < advantageChance)
                {
                    var secondRoll = RollFishFromPool(pool, settings, boosts, activeFrenzy);
                    if (secondRoll.Rarity > selectedFish.Rarity ||
                        (secondRoll.Rarity == selectedFish.Rarity && secondRoll.BaseGold > selectedFish.BaseGold))
                    {
                        selectedFish = secondRoll;
                        advantageUpgraded = true;
                    }
                }
            }

            // 3. Record visible boost attributions for overlay & player feedback
            if (attractingBoostItem?.ShopItem != null && attractedCategory != null)
            {
                triggeredBoosts.Add(CreateTriggeredInfo(attractingBoostItem, $"Attracted {attractedCategory}"));
            }

            foreach (var boost in boosts)
            {
                if (boost.ShopItem == null) continue;
                var hasSpecificFishMatch =
                    (boost.ShopItem.BoostType == FishingBoostType.SpecificFishBoost ||
                     boost.ShopItem.BoostType2 == FishingBoostType.SpecificFishBoost ||
                     boost.ShopItem.BoostType3 == FishingBoostType.SpecificFishBoost) &&
                    boost.ShopItem.TargetFishTypeId == selectedFish.Id;

                if (hasSpecificFishMatch)
                {
                    triggeredBoosts.Add(CreateTriggeredInfo(boost, $"Targeted {selectedFish.Name}"));
                }
            }

            if (advantageUpgraded && primaryConsumableRarityItem?.ShopItem != null)
            {
                triggeredBoosts.Add(CreateTriggeredInfo(primaryConsumableRarityItem, "Upgraded Rarity!"));
            }
            else if (selectedFish.Rarity >= FishRarity.Rare && primaryConsumableRarityItem?.ShopItem != null)
            {
                triggeredBoosts.Add(CreateTriggeredInfo(primaryConsumableRarityItem, "Rarity Boost"));
            }

            return new FishingSelectionResult
            {
                Fish = selectedFish,
                TriggeredBoosts = triggeredBoosts,
                WasFrenzyInfluenced = wasFrenzyInfluenced
            };
        }

        /// <summary>
        /// Computes each fish's effective weight across the pool:
        /// - GeneralRarityBoost uses Progressive Rarity Scaling (higher rarities scale stronger).
        /// - SpecificFishBoost ONLY multiplies that single target fish's individual weight (never inflating other fish in that rarity).
        /// </summary>
        internal static List<(FishType Fish, double Weight)> ComputeEffectiveFishWeights(
            List<FishType> pool,
            FishingSettings? settings,
            List<UserFishingBoost> boosts,
            FishingFrenzyState? activeFrenzy = null)
        {
            var rarityWeights = FishingRarityWeightProfiles.CreateAvailableWeights(pool, normalizeToBaseTotal: true);

            if (settings?.BoostMode == true)
            {
                FishingRarityWeightProfiles.ApplyGlobalRarityMultiplier(rarityWeights, settings.BoostModeRarityMultiplier);
            }

            if (activeFrenzy != null && activeFrenzy.RarityMultiplier > 1.0)
            {
                FishingRarityWeightProfiles.ApplyGlobalRarityMultiplier(rarityWeights, activeFrenzy.RarityMultiplier);
            }

            foreach (var boost in boosts)
            {
                ApplyBoostToRarityWeights(rarityWeights, boost.ShopItem?.BoostType, boost.ShopItem?.BoostAmount ?? 0);
                ApplyBoostToRarityWeights(rarityWeights, boost.ShopItem?.BoostType2, boost.ShopItem?.BoostAmount2 ?? 0);
                ApplyBoostToRarityWeights(rarityWeights, boost.ShopItem?.BoostType3, boost.ShopItem?.BoostAmount3 ?? 0);
            }

            var countByRarity = pool
                .GroupBy(f => f.Rarity)
                .ToDictionary(g => g.Key, g => g.Count());

            var specificFishBoosts = boosts.Where(b =>
                b.ShopItem != null &&
                b.ShopItem.TargetFishTypeId.HasValue &&
                (b.ShopItem.BoostType == FishingBoostType.SpecificFishBoost ||
                 b.ShopItem.BoostType2 == FishingBoostType.SpecificFishBoost ||
                 b.ShopItem.BoostType3 == FishingBoostType.SpecificFishBoost)).ToList();

            var result = new List<(FishType Fish, double Weight)>(pool.Count);
            foreach (var fish in pool)
            {
                if (!rarityWeights.TryGetValue(fish.Rarity, out var tierWeight) || tierWeight <= 0)
                {
                    continue;
                }

                var countInTier = Math.Max(1, countByRarity.GetValueOrDefault(fish.Rarity, 1));
                var individualWeight = tierWeight / countInTier;

                // Single-fish-only cross-rarity boost (does NOT inflate other fish in the same rarity!)
                foreach (var boost in specificFishBoosts)
                {
                    if (boost.ShopItem!.TargetFishTypeId == fish.Id)
                    {
                        individualWeight *= GetSpecificFishOnlyMultiplier(boost.ShopItem);
                    }
                }

                result.Add((fish, Math.Max(0.0001, individualWeight)));
            }

            return result;
        }

        private static FishType RollFishFromPool(
            List<FishType> pool,
            FishingSettings? settings,
            List<UserFishingBoost> boosts,
            FishingFrenzyState? activeFrenzy)
        {
            var weightedFish = ComputeEffectiveFishWeights(pool, settings, boosts, activeFrenzy);
            var totalWeight = weightedFish.Sum(w => w.Weight);
            if (totalWeight <= 0 || weightedFish.Count == 0)
            {
                return pool[RandomNumberGenerator.GetInt32(pool.Count)];
            }

            var roll = NextUnitDouble() * totalWeight;
            var running = 0.0;
            foreach (var (fish, weight) in weightedFish)
            {
                running += weight;
                if (roll < running)
                {
                    return fish;
                }
            }

            return weightedFish[^1].Fish;
        }

        internal static double GetTargetedBoostMultiplier(FishingShopItem? shopItem, FishType fish)
        {
            if (shopItem == null) return 1.0;

            var categoryMatchesTarget = !string.IsNullOrWhiteSpace(shopItem.TargetCategory) &&
                FishHasCategory(fish, shopItem.TargetCategory);

            return ApplyTargetedBoosts(
                [
                    (shopItem.BoostType, shopItem.BoostAmount),
                    (shopItem.BoostType2, shopItem.BoostAmount2 ?? 0),
                    (shopItem.BoostType3, shopItem.BoostAmount3 ?? 0)
                ],
                shopItem.TargetFishTypeId == fish.Id,
                categoryMatchesTarget);
        }

        private static double GetSpecificFishOnlyMultiplier(FishingShopItem shopItem)
        {
            var mult = 1.0;
            if (shopItem.BoostType == FishingBoostType.SpecificFishBoost)
                mult *= Math.Max(0.1, 1.0 + shopItem.BoostAmount);
            if (shopItem.BoostType2 == FishingBoostType.SpecificFishBoost && shopItem.BoostAmount2.HasValue)
                mult *= Math.Max(0.1, 1.0 + shopItem.BoostAmount2.Value);
            if (shopItem.BoostType3 == FishingBoostType.SpecificFishBoost && shopItem.BoostAmount3.HasValue)
                mult *= Math.Max(0.1, 1.0 + shopItem.BoostAmount3.Value);
            return mult;
        }

        private static double ApplyTargetedBoosts(
            (FishingBoostType? Type, double Amount)[] boosts,
            bool fishMatchesTarget,
            bool categoryMatchesTarget)
        {
            var multiplier = 1.0;
            foreach (var boost in boosts)
            {
                var matchesFish = boost.Type == FishingBoostType.SpecificFishBoost && fishMatchesTarget;
                var matchesCategory = boost.Type == FishingBoostType.SpecificCategoryBoost && categoryMatchesTarget;
                if (matchesFish || matchesCategory)
                {
                    multiplier *= Math.Max(0.1, 1.0 + boost.Amount);
                }
            }

            return multiplier;
        }

        private static List<(UserFishingBoost Boost, string Category, double Amount)> GetCategoryBoostEntries(List<UserFishingBoost> boosts)
        {
            var entries = new List<(UserFishingBoost, string, double)>();
            foreach (var boost in boosts)
            {
                var item = boost.ShopItem;
                if (item == null || string.IsNullOrWhiteSpace(item.TargetCategory)) continue;

                var totalForBoost = 0.0;
                if (item.BoostType == FishingBoostType.SpecificCategoryBoost)
                    totalForBoost += item.BoostAmount;
                if (item.BoostType2 == FishingBoostType.SpecificCategoryBoost)
                    totalForBoost += item.BoostAmount2 ?? 0;
                if (item.BoostType3 == FishingBoostType.SpecificCategoryBoost)
                    totalForBoost += item.BoostAmount3 ?? 0;

                if (totalForBoost > 0)
                {
                    entries.Add((boost, item.TargetCategory.Trim(), totalForBoost));
                }
            }
            return entries;
        }

        private static double GetConsumableGeneralRarityBoost(List<UserFishingBoost> boosts, out UserFishingBoost? primaryItem)
        {
            primaryItem = null;
            var bestSingle = 0.0;
            var total = 0.0;

            foreach (var boost in boosts)
            {
                var item = boost.ShopItem;
                if (item == null || !item.MaxUses.HasValue) continue;

                var itemAmount = 0.0;
                if (item.BoostType == FishingBoostType.GeneralRarityBoost && item.BoostAmount > 0)
                    itemAmount += item.BoostAmount;
                if (item.BoostType2 == FishingBoostType.GeneralRarityBoost && (item.BoostAmount2 ?? 0) > 0)
                    itemAmount += item.BoostAmount2!.Value;
                if (item.BoostType3 == FishingBoostType.GeneralRarityBoost && (item.BoostAmount3 ?? 0) > 0)
                    itemAmount += item.BoostAmount3!.Value;

                if (itemAmount > 0)
                {
                    total += itemAmount;
                    if (itemAmount > bestSingle)
                    {
                        bestSingle = itemAmount;
                        primaryItem = boost;
                    }
                }
            }

            return total;
        }

        private static FishingTriggeredBoostInfo CreateTriggeredInfo(UserFishingBoost boost, string reason)
        {
            var item = boost.ShopItem!;
            int? remainingAfterUse = item.MaxUses.HasValue && boost.RemainingUses > 0
                ? Math.Max(0, boost.RemainingUses - 1)
                : (item.MaxUses.HasValue ? 0 : null);

            return new FishingTriggeredBoostInfo
            {
                ItemName = item.Name,
                Reason = reason,
                RemainingUses = remainingAfterUse,
                MaxUses = item.MaxUses
            };
        }

        private static bool FishHasCategory(FishType fish, string category)
        {
            return fish.Categories.Any(c => c.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        private static void ApplyBoostToRarityWeights(
            Dictionary<FishRarity, double> rarityWeights,
            FishingBoostType? boostType,
            double boostAmount)
        {
            if (boostType == FishingBoostType.GeneralRarityBoost)
            {
                FishingRarityWeightProfiles.ApplyProgressiveGeneralRarityBoost(rarityWeights, boostAmount);
            }
        }

        public static int CalculateStars(FishType fishType, List<UserFishingBoost> boosts)
        {
            var boostAmount = GetTotalBoostAmount(boosts, FishingBoostType.StarBoost);

            var threeStarChance = 5.0 + (boostAmount * 100);
            var twoStarChance = 20.0 + (boostAmount * 100);

            var roll = RandomNumberGenerator.GetInt32(0, 10000) / 100.0;
            if (roll < threeStarChance) return 3;
            if (roll < twoStarChance + threeStarChance) return 2;
            return 1;
        }

        public static double CalculateWeight(FishType fishType, int stars, List<UserFishingBoost> boosts)
        {
            // Clamp to a positive floor so stacked negative WeightBoost items can't zero out or invert weight.
            var boostMultiplier = Math.Max(0.01, 1.0 + GetTotalBoostAmount(boosts, FishingBoostType.WeightBoost));

            // Star multipliers increase weight for higher quality catches
            var starMultiplier = stars switch
            {
                3 => 1.5,
                2 => 1.2,
                _ => 1.0
            };

            // Generate random weight between 0.8x and 1.13x of base weight
            var minMultiplier = 0.8;
            var maxMultiplier = 1.13;
            var randomValue = RandomNumberGenerator.GetInt32(0, 10000) / 10000.0;
            var weightMultiplier = minMultiplier + (randomValue * (maxMultiplier - minMultiplier));

            var weight = fishType.BaseWeight * weightMultiplier * starMultiplier * boostMultiplier;
            return Math.Max(0.01, Math.Round(weight, 2));
        }

        public static int CalculateGold(FishType fishType, int stars, double actualWeight)
        {
            // Star levels determine consecutive, non-overlapping gold ranges
            var (minMultiplier, maxMultiplier) = stars switch
            {
                3 => (1.25, 1.41),
                2 => (1.0, 1.25),
                _ => (0.75, 1.0)
            };

            var randomValue = RandomNumberGenerator.GetInt32((int)(minMultiplier * 1000), (int)(maxMultiplier * 1000)) / 1000.0;

            // Weight influences gold: heavier fish relative to base weight = more gold
            var weightMultiplier = 1.0;
            if (fishType.BaseWeight > 0)
            {
                weightMultiplier = 0.9 + ((actualWeight / fishType.BaseWeight - 0.8) / (1.13 - 0.8) * 0.165);
                weightMultiplier = Math.Max(0.9, Math.Min(1.065, weightMultiplier));
            }

            return Math.Max(1, (int)(fishType.BaseGold * randomValue * weightMultiplier));
        }

        public static List<FishingTriggeredBoostInfo> GetWeightBoostAttributions(List<UserFishingBoost> boosts)
        {
            var results = new List<FishingTriggeredBoostInfo>();
            foreach (var boost in boosts)
            {
                var item = boost.ShopItem;
                if (item == null || !item.MaxUses.HasValue) continue;

                var weightAmount = 0.0;
                if (item.BoostType == FishingBoostType.WeightBoost) weightAmount += item.BoostAmount;
                if (item.BoostType2 == FishingBoostType.WeightBoost) weightAmount += item.BoostAmount2 ?? 0;
                if (item.BoostType3 == FishingBoostType.WeightBoost) weightAmount += item.BoostAmount3 ?? 0;

                if (Math.Abs(weightAmount) >= 0.01)
                {
                    var pct = (int)Math.Round(weightAmount * 100);
                    var label = pct > 0 ? $"+{pct}% Weight" : $"{pct}% Weight";
                    results.Add(CreateTriggeredInfo(boost, label));
                }
            }
            return results;
        }

        private static double GetTotalBoostAmount(List<UserFishingBoost> boosts, FishingBoostType targetType)
        {
            var amount = 0.0;

            foreach (var boost in boosts)
            {
                if (boost.ShopItem?.BoostType == targetType)
                    amount += boost.ShopItem.BoostAmount;
                if (boost.ShopItem?.BoostType2 == targetType)
                    amount += boost.ShopItem.BoostAmount2 ?? 0;
                if (boost.ShopItem?.BoostType3 == targetType)
                    amount += boost.ShopItem.BoostAmount3 ?? 0;
            }

            return amount;
        }

        private static double NextUnitDouble() =>
            RandomNumberGenerator.GetInt32(0, 1_000_000) / 1_000_000.0;

        /// <summary>
        /// Determines whether a fish is eligible for a tournament based on its explicitly selected fish types,
        /// selected categories, or (when neither are set) all fish are eligible by default.
        /// </summary>
        public static bool IsFishEligible(FishingTournament tournament, Guid fishTypeId, IEnumerable<string> fishCategoryNames)
        {
            var hasEligibleFish = tournament.EligibleFish.Count > 0;
            var hasEligibleCategories = tournament.EligibleCategories.Count > 0;

            if (!hasEligibleFish && !hasEligibleCategories)
            {
                return true;
            }

            if (hasEligibleFish && tournament.EligibleFish.Any(fish => fish.FishTypeId == fishTypeId))
            {
                return true;
            }

            if (hasEligibleCategories)
            {
                if (fishCategoryNames.Any(category => tournament.EligibleCategories.Any(selected =>
                    string.Equals(selected.Category, category, StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
