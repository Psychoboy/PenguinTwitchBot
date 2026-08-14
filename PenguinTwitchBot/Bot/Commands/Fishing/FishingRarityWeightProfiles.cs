using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    internal static class FishingRarityWeightProfiles
    {
        private static readonly double BaseRarityWeightTotal;

        private static readonly IReadOnlyDictionary<FishRarity, double> BaseRarityWeights =
            new Dictionary<FishRarity, double>
            {
                { FishRarity.Common, 50.0 },
                { FishRarity.Uncommon, 30.0 },
                { FishRarity.Rare, 15.0 },
                { FishRarity.Epic, 4.0 },
                { FishRarity.Legendary, 1.0 },
                { FishRarity.Mythical, 0.2 }
            };

        private static readonly FishRarity[] BoostableRarities =
        [
            FishRarity.Uncommon,
            FishRarity.Rare,
            FishRarity.Epic,
            FishRarity.Legendary,
            FishRarity.Mythical
        ];

        static FishingRarityWeightProfiles()
        {
            BaseRarityWeightTotal = BaseRarityWeights.Values.Sum();
        }

        public static Dictionary<FishRarity, double> CreateBaseWeights()
        {
            return BaseRarityWeights.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public static Dictionary<FishRarity, double> CreateAvailableWeights(IEnumerable<FishType> fishTypes, bool normalizeToBaseTotal = false)
        {
            var rarityWeights = CreateBaseWeights();
            var availableRarities = fishTypes.Select(f => f.Rarity).ToHashSet();

            foreach (var rarity in rarityWeights.Keys.Except(availableRarities).ToList())
            {
                rarityWeights.Remove(rarity);
            }

            if (normalizeToBaseTotal && rarityWeights.Count > 0)
            {
                var currentTotal = rarityWeights.Values.Sum();
                if (currentTotal > 0)
                {
                    var multiplier = BaseRarityWeightTotal / currentTotal;
                    foreach (var rarity in rarityWeights.Keys.ToList())
                    {
                        rarityWeights[rarity] *= multiplier;
                    }
                }
            }

            return rarityWeights;
        }

        public static void ApplyGlobalRarityMultiplier(Dictionary<FishRarity, double> rarityWeights, double multiplier)
        {
            foreach (var rarity in BoostableRarities)
            {
                if (rarityWeights.ContainsKey(rarity))
                {
                    rarityWeights[rarity] *= multiplier;
                }
            }
        }
    }
}
