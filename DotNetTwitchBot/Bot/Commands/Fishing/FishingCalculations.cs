using DotNetTwitchBot.Bot.Models.Fishing;
using System.Security.Cryptography;
using DotNetTwitchBot.Extensions;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    /// <summary>
    /// Shared calculation logic for fishing mechanics (rarity selection, stars, weight, gold)
    /// </summary>
    internal static class FishingCalculations
    {
        public static FishType SelectRandomFish(List<FishType> fishTypes, FishingSettings? settings, List<UserFishingBoost> boosts)
        {
            var rarityWeights = new Dictionary<FishRarity, double>
            {
                { FishRarity.Common, 50.0 },
                { FishRarity.Uncommon, 30.0 },
                { FishRarity.Rare, 15.0 },
                { FishRarity.Epic, 4.0 },
                { FishRarity.Legendary, 1.0 }
            };

            if (settings?.BoostMode == true)
            {
                var multiplier = settings.BoostModeRarityMultiplier;
                rarityWeights[FishRarity.Uncommon] *= multiplier;
                rarityWeights[FishRarity.Rare] *= multiplier;
                rarityWeights[FishRarity.Epic] *= multiplier;
                rarityWeights[FishRarity.Legendary] *= multiplier;
            }

            foreach (var boost in boosts)
            {
                ApplyBoostToRarityWeights(rarityWeights, fishTypes, boost, boost.ShopItem?.BoostType, boost.ShopItem?.BoostAmount ?? 0, boost.ShopItem?.TargetFishTypeId);
                ApplyBoostToRarityWeights(rarityWeights, fishTypes, boost, boost.ShopItem?.BoostType2, boost.ShopItem?.BoostAmount2 ?? 0, boost.ShopItem?.TargetFishTypeId);
                ApplyBoostToRarityWeights(rarityWeights, fishTypes, boost, boost.ShopItem?.BoostType3, boost.ShopItem?.BoostAmount3 ?? 0, boost.ShopItem?.TargetFishTypeId);
            }

            var totalWeight = rarityWeights.Values.Sum();
            var randomValue = RandomNumberGenerator.GetInt32(0, (int)(totalWeight * 1000)) / 1000.0;
            var currentWeight = 0.0;
            var selectedRarity = FishRarity.Common;

            foreach (var kvp in rarityWeights.OrderBy(k => (int)k.Key))
            {
                currentWeight += kvp.Value;
                if (randomValue < currentWeight)
                {
                    selectedRarity = kvp.Key;
                    break;
                }
            }

            var fishOfRarity = fishTypes.Where(f => f.Rarity == selectedRarity).ToList();
            if (fishOfRarity.Count == 0)
            {
                fishOfRarity = fishTypes;
            }

            // Get all specific fish boosts from all boost type slots
            var specificBoosts = boosts.Where(b => 
                (b.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost ||
                 b.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost ||
                 b.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost) &&
                b.ShopItem.TargetFishTypeId != null).ToList();

            if (specificBoosts.Any())
            {
                var weightedFish = new List<(FishType fish, double weight)>();
                foreach (var fish in fishOfRarity)
                {
                    var weight = 1.0;
                    // Apply all specific boosts targeting this fish
                    foreach (var boost in specificBoosts.Where(b => b.ShopItem?.TargetFishTypeId == fish.Id))
                    {
                        if (boost.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost)
                        {
                            weight *= (1.0 + boost.ShopItem.BoostAmount);
                        }
                        if (boost.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost)
                        {
                            weight *= (1.0 + (boost.ShopItem.BoostAmount2 ?? 0));
                        }
                        if (boost.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost)
                        {
                            weight *= (1.0 + (boost.ShopItem.BoostAmount3 ?? 0));
                        }
                    }
                    weightedFish.Add((fish, weight));
                }

                var totalFishWeight = weightedFish.Sum(w => w.weight);
                var randomFishValue = RandomNumberGenerator.GetInt32(0, (int)(totalFishWeight * 1000)) / 1000.0;
                var currentFishWeight = 0.0;

                foreach (var (fish, weight) in weightedFish)
                {
                    currentFishWeight += weight;
                    if (randomFishValue < currentFishWeight)
                    {
                        return fish;
                    }
                }
            }

            return fishOfRarity.RandomElement();
        }

        private static void ApplyBoostToRarityWeights(
            Dictionary<FishRarity, double> rarityWeights, 
            List<FishType> fishTypes, 
            UserFishingBoost boost, 
            FishingBoostType? boostType, 
            double boostAmount,
            int? targetFishTypeId)
        {
            if (boostType == FishingBoostType.GeneralRarityBoost)
            {
                foreach (var rarity in rarityWeights.Keys.ToList())
                {
                    if (rarity != FishRarity.Common)
                    {
                        rarityWeights[rarity] *= (1.0 + boostAmount);
                    }
                }
            }
            else if (boostType == FishingBoostType.SpecificFishBoost && targetFishTypeId != null)
            {
                var targetFish = fishTypes.FirstOrDefault(f => f.Id == targetFishTypeId);
                if (targetFish != null)
                {
                    rarityWeights[targetFish.Rarity] *= (1.0 + boostAmount);
                }
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
            var boostMultiplier = 1.0 + GetTotalBoostAmount(boosts, FishingBoostType.WeightBoost);

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
            return Math.Round(weight, 2);
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
    }
}
