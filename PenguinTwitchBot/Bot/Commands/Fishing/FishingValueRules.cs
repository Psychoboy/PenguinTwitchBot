using System.Diagnostics.CodeAnalysis;
using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public static class FishingValueRules
    {
        public const double MinBoostAmount = -0.8;
        public const double MaxBoostAmount = 5.0;

        public static void NormalizeAndValidate(FishType fishType)
        {
            if (double.IsNaN(fishType.BaseWeight) || double.IsInfinity(fishType.BaseWeight) || fishType.BaseWeight < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fishType.BaseWeight), "Base weight cannot be negative.");
            }

            fishType.BaseWeight = Math.Round(fishType.BaseWeight, 2, MidpointRounding.AwayFromZero);
        }

        public static void NormalizeAndValidate(FishingShopItem item)
        {
            item.BoostAmount = Normalize(item.BoostAmount, MinBoostAmount, MaxBoostAmount, nameof(item.BoostAmount));
            item.BoostAmount2 = NormalizeNullable(item.BoostAmount2, MinBoostAmount, MaxBoostAmount, nameof(item.BoostAmount2));
            item.BoostAmount3 = NormalizeNullable(item.BoostAmount3, MinBoostAmount, MaxBoostAmount, nameof(item.BoostAmount3));

            NormalizeShopItem(item);

            if (!ValidateShopItemTargets(item, out var errorMessage))
            {
                var paramName = UsesSpecificFishBoost(item) && !item.TargetFishTypeId.HasValue
                    ? nameof(item.TargetFishTypeId)
                    : nameof(item.TargetCategory);
                throw new ArgumentException(errorMessage, paramName);
            }
        }

        public static bool UsesBoostType(FishingShopItem item, FishingBoostType boostType) =>
            item.BoostType == boostType || item.BoostType2 == boostType || item.BoostType3 == boostType;

        public static bool UsesSpecificFishBoost(FishingShopItem item) =>
            UsesBoostType(item, FishingBoostType.SpecificFishBoost);

        public static bool UsesSpecificCategoryBoost(FishingShopItem item) =>
            UsesBoostType(item, FishingBoostType.SpecificCategoryBoost);

        public static double ClampBoostAmount(double value) =>
            Math.Round(Math.Clamp(value, MinBoostAmount, MaxBoostAmount), 2, MidpointRounding.AwayFromZero);

        public static (FishingBoostType? Type, double? Amount) NormalizeOptionalBoost(FishingBoostType? boostType, double? boostAmount)
        {
            if (!boostType.HasValue)
            {
                return (null, null);
            }

            var amount = boostAmount.HasValue ? ClampBoostAmount(boostAmount.Value) : 0;
            return (boostType, amount);
        }

        public static (FishingBoostType? Type, double? Amount) ResolveOptionalBoost(
            string? rawType,
            double? newAmount,
            FishingBoostType? currentType,
            double? currentAmount)
        {
            if (!string.IsNullOrWhiteSpace(rawType) &&
                !rawType.Equals("KeepCurrent", StringComparison.OrdinalIgnoreCase))
            {
                if (rawType.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    currentType = null;
                    currentAmount = null;
                }
                else if (Enum.TryParse<FishingBoostType>(rawType, true, out var parsedType))
                {
                    currentType = parsedType;
                    currentAmount ??= 0;
                }
            }

            if (newAmount.HasValue)
            {
                currentAmount = ClampBoostAmount(newAmount.Value);
            }

            return NormalizeOptionalBoost(currentType, currentAmount);
        }

        public static void NormalizeShopItemTargets(FishingShopItem item)
        {
            item.TargetCategory = string.IsNullOrWhiteSpace(item.TargetCategory) ? null : item.TargetCategory.Trim();
        }

        public static void ClearUnusedShopItemTargets(FishingShopItem item)
        {
            if (!UsesSpecificFishBoost(item))
            {
                item.TargetFishTypeId = null;
            }

            if (!UsesSpecificCategoryBoost(item))
            {
                item.TargetCategory = null;
            }
        }

        public static void NormalizeShopItem(FishingShopItem item)
        {
            item.BoostAmount = ClampBoostAmount(item.BoostAmount);
            (item.BoostType2, item.BoostAmount2) = NormalizeOptionalBoost(item.BoostType2, item.BoostAmount2);
            (item.BoostType3, item.BoostAmount3) = NormalizeOptionalBoost(item.BoostType3, item.BoostAmount3);
            NormalizeShopItemTargets(item);
        }

        public static bool ValidateShopItemTargets(FishingShopItem item, [NotNullWhen(false)] out string? errorMessage)
        {
            if (UsesSpecificFishBoost(item) && !item.TargetFishTypeId.HasValue)
            {
                errorMessage = "Target fish is required when using a specific fish boost.";
                return false;
            }

            if (UsesSpecificCategoryBoost(item) && string.IsNullOrWhiteSpace(item.TargetCategory))
            {
                errorMessage = "Target category is required when using a specific category boost.";
                return false;
            }

            errorMessage = null;
            return true;
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