using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    internal static class FishingRarityThresholdRules
    {
        public static FishRarity CalculateRarityFromGold(int baseGold, FishingSettings settings)
        {
            return baseGold switch
            {
                var gold when gold >= settings.RarityMythicalThreshold => FishRarity.Mythical,
                var gold when gold >= settings.RarityLegendaryThreshold => FishRarity.Legendary,
                var gold when gold >= settings.RarityEpicThreshold => FishRarity.Epic,
                var gold when gold >= settings.RarityRareThreshold => FishRarity.Rare,
                var gold when gold >= settings.RarityUncommonThreshold => FishRarity.Uncommon,
                _ => FishRarity.Common
            };
        }

        public static bool TryValidateThresholdOrder(FishingSettings settings, out string message)
        {
            if (settings.RarityUncommonThreshold < settings.RarityRareThreshold &&
                settings.RarityRareThreshold < settings.RarityEpicThreshold &&
                settings.RarityEpicThreshold < settings.RarityLegendaryThreshold &&
                settings.RarityLegendaryThreshold < settings.RarityMythicalThreshold)
            {
                message = string.Empty;
                return true;
            }

            message = "Rarity thresholds must be strictly increasing: Uncommon < Rare < Epic < Legendary < Mythical.";
            return false;
        }
    }
}
