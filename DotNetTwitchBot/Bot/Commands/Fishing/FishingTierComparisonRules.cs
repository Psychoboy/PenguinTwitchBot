using DotNetTwitchBot.Bot.Models.Fishing;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public enum TierComparisonResult
    {
        None,
        New,
        Equipped,
        Upgrade,
        Downgrade,
        Sidegrade
    }

    public static class FishingTierComparisonRules
    {
        public static TierComparisonResult CompareToEquipped(
            FishingShopItem item,
            IReadOnlyDictionary<EquipmentSlot, FishingShopItem> equippedShopItemBySlot,
            IReadOnlyDictionary<int, EquipmentTier> tierMap)
        {
            if (item.IsConsumable || !item.EquipmentSlot.HasValue)
            {
                return TierComparisonResult.None;
            }

            if (!equippedShopItemBySlot.TryGetValue(item.EquipmentSlot.Value, out var equippedItem))
            {
                return TierComparisonResult.New;
            }

            if (equippedItem.Id == item.Id)
            {
                return TierComparisonResult.Equipped;
            }

            var itemTier = tierMap.TryGetValue(item.Id, out var computedItemTier) ? computedItemTier : item.GetTier();
            var equippedTier = tierMap.TryGetValue(equippedItem.Id, out var computedEquippedTier) ? computedEquippedTier : equippedItem.GetTier();

            if (itemTier > equippedTier)
            {
                return TierComparisonResult.Upgrade;
            }

            if (itemTier < equippedTier)
            {
                return TierComparisonResult.Downgrade;
            }

            return TierComparisonResult.Sidegrade;
        }
    }
}
