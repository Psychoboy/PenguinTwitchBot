namespace PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations
{
    public class FishingItemBrokenTriggerConfiguration
    {
        /// <summary>
        /// Filter by EquipmentSlot names (e.g. "Rod", "Reel"). Empty = all slots.
        /// </summary>
        public List<string> EquipmentSlots { get; set; } = [];

        /// <summary>
        /// Filter by specific ShopItemIds. Empty = any item.
        /// </summary>
        public List<int> ShopItemIds { get; set; } = [];
    }
}
