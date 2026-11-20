using PenguinTwitchBot.Database.Bot.Models.Fishing;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public static class FishingCatchRewardCalculator
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static List<FishingCatchRewardRule> ParseRules(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<FishingCatchRewardRule>>(json, JsonOptions) ?? [];
            }
            catch
            {
                return [];
            }
        }

        public static string SerializeRules(IEnumerable<FishingCatchRewardRule> rules) =>
            JsonSerializer.Serialize(rules.ToList(), JsonOptions);

        public static List<QuickTournamentRewardEntry> ParseQuickTournamentRewards(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<QuickTournamentRewardEntry>>(json, JsonOptions) ?? [];
            }
            catch
            {
                return [];
            }
        }

        public static string SerializeQuickTournamentRewards(IEnumerable<QuickTournamentRewardEntry> entries) =>
            JsonSerializer.Serialize(entries.ToList(), JsonOptions);

        public static long EvaluateRule(
            FishingCatchRewardRule rule,
            FishRarity rarity,
            IEnumerable<string> categories,
            int stars,
            double weight,
            int gold,
            bool isNewSpecies,
            bool isPersonalBest)
        {
            if (!rule.Enabled || rule.PointTypeId == Guid.Empty)
            {
                return 0;
            }

            if (rule.MinRarity.HasValue && rarity < rule.MinRarity.Value)
            {
                return 0;
            }

            if (rule.MaxRarity.HasValue && rarity > rule.MaxRarity.Value)
            {
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(rule.CategoryFilter) &&
                !categories.Any(c => string.Equals(c, rule.CategoryFilter.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return 0;
            }

            var starMult = stars switch
            {
                3 => rule.Star3Multiplier,
                2 => rule.Star2Multiplier,
                _ => rule.Star1Multiplier
            };

            double rawAmount = rule.Mode switch
            {
                FishingCatchRewardMode.Flat => rule.FlatAmount,
                FishingCatchRewardMode.WeightScaled => weight * rule.BaseMultiplier * starMult,
                _ => gold * rule.BaseMultiplier * starMult
            };

            var bonusMult = 1.0;
            if (isNewSpecies && rule.NewDexBonusPercent > 0)
            {
                bonusMult += rule.NewDexBonusPercent / 100.0;
            }
            if (isPersonalBest && rule.PersonalBestBonusPercent > 0)
            {
                bonusMult += rule.PersonalBestBonusPercent / 100.0;
            }

            rawAmount *= bonusMult;

            var amount = (long)Math.Round(rawAmount);
            if (rule.MinAmount.HasValue && amount < rule.MinAmount.Value)
            {
                amount = rule.MinAmount.Value;
            }
            if (rule.MaxAmount.HasValue && rule.MaxAmount.Value > 0 && amount > rule.MaxAmount.Value)
            {
                amount = rule.MaxAmount.Value;
            }

            return Math.Max(0, amount);
        }
    }
}
