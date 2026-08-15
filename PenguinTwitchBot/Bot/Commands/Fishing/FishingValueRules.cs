using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    internal static class FishingValueRules
    {
        public static void NormalizeAndValidate(FishType fishType)
        {
            if (double.IsNaN(fishType.BaseWeight) || double.IsInfinity(fishType.BaseWeight) || fishType.BaseWeight < 0.05)
            {
                throw new ArgumentOutOfRangeException(nameof(fishType.BaseWeight), "Base weight must be at least 0.05.");
            }

            fishType.BaseWeight = Math.Round(fishType.BaseWeight, 2, MidpointRounding.AwayFromZero);
        }

        public static void NormalizeAndValidate(FishingShopItem item)
        {
            item.BoostAmount = Normalize(item.BoostAmount, -0.8, 5.0, nameof(item.BoostAmount));
            item.BoostAmount2 = NormalizeNullable(item.BoostAmount2, -0.8, 5.0, nameof(item.BoostAmount2));
            item.BoostAmount3 = NormalizeNullable(item.BoostAmount3, -0.8, 5.0, nameof(item.BoostAmount3));
        }

        private static double Normalize(double value, double minimum, double maximum, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(name, $"Value must be between {minimum} and {maximum}.");
            }

            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static double? NormalizeNullable(double? value, double minimum, double maximum, string name) =>
            value.HasValue ? Normalize(value.Value, minimum, maximum, name) : null;
    }
}