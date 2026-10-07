using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public enum SellEligibilityReason
    {
        Eligible,
        ItemNotFound,
        EquippedItem,
        LimitedUses,
        Consumable
    }

    public static class FishingInventorySellRules
    {
        public const double SellRate = 0.15;

        public static int GetSellPrice(FishingShopItem? shopItem, UserFishingBoost? boost = null)
        {
            if (shopItem == null)
            {
                return 0;
            }

            var basePrice = shopItem.Cost * SellRate;
            if (boost?.CurrentDurability.HasValue == true && shopItem.MaxDurability.HasValue && shopItem.MaxDurability.Value > 0)
            {
                var durabilityRatio = Math.Clamp(boost.CurrentDurability.Value / shopItem.MaxDurability.Value, 0.0, 1.0);
                return (int)Math.Floor(basePrice * durabilityRatio);
            }

            return (int)basePrice;
        }

        public static SellEligibilityReason GetSellEligibility(UserFishingBoost? boost, FishingShopItem? shopItem = null)
        {
            if (boost == null)
            {
                return SellEligibilityReason.ItemNotFound;
            }

            var resolvedShopItem = shopItem ?? boost.ShopItem;
            if (resolvedShopItem == null)
            {
                return SellEligibilityReason.ItemNotFound;
            }

            if (boost.IsEquipped)
            {
                return SellEligibilityReason.EquippedItem;
            }

            if (boost.RemainingUses >= 0)
            {
                return SellEligibilityReason.LimitedUses;
            }

            if (resolvedShopItem.MaxUses.HasValue)
            {
                return SellEligibilityReason.Consumable;
            }

            return SellEligibilityReason.Eligible;
        }

        public static bool CanSell(UserFishingBoost? boost, FishingShopItem? shopItem = null)
        {
            return GetSellEligibility(boost, shopItem) == SellEligibilityReason.Eligible;
        }

        public static string GetSellFailureMessage(SellEligibilityReason reason)
        {
            return reason switch
            {
                SellEligibilityReason.ItemNotFound => "Item not found",
                SellEligibilityReason.EquippedItem => "Cannot sell an equipped item",
                SellEligibilityReason.LimitedUses => "Cannot sell items with limited usage",
                SellEligibilityReason.Consumable => "Cannot sell consumable items",
                _ => "This item cannot be sold"
            };
        }
    }
}
