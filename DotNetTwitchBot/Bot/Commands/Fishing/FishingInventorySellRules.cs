using DotNetTwitchBot.Bot.Models.Fishing;

namespace DotNetTwitchBot.Bot.Commands.Fishing
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

        public static int GetSellPrice(FishingShopItem? shopItem)
        {
            return shopItem == null ? 0 : (int)(shopItem.Cost * SellRate);
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

            if (resolvedShopItem.IsConsumable)
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
