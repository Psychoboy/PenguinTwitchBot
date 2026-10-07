using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
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
            if (item.MaxUses.HasValue || !item.EquipmentSlot.HasValue)
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

            var itemTier = tierMap.GetValueOrDefault(item.Id, EquipmentTier.Entry);
            var equippedTier = tierMap.GetValueOrDefault(equippedItem.Id, EquipmentTier.Entry);

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
