namespace PenguinTwitchBot.Database.Bot.Models.Fishing
{
    public class FishingBrokenItemInfo
    {
        public int UserBoostId { get; set; }
        public int ShopItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public EquipmentSlot? EquipmentSlot { get; set; }
        public int ItemCost { get; set; }
        public bool WasReplaced { get; set; }
        public string? ReplacementItemName { get; set; }
    }
}
