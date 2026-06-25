using System.ComponentModel.DataAnnotations;

namespace PenguinTwitchBot.Bot.Models.Fishing
{
    public class FishingSnapEvent
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(255)]
        public string UserId { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        [MaxLength(16)]
        public string SnapType { get; set; } = "Line";

        public decimal TotalGoldLost { get; set; }

        public int LostItemCount { get; set; }

        public string LostItemsJson { get; set; } = "[]";

        public DateTime SnappedAt { get; set; } = DateTime.UtcNow;
    }

    public class FishingSnapLossResult
    {
        public decimal TotalGoldLost { get; set; }
        public List<FishingSnapLostItem> LostItems { get; set; } = new();
    }

    public class FishingSnapLostItem
    {
        public int UserBoostId { get; set; }
        public int ShopItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string EquipmentSlot { get; set; } = string.Empty;
        public int ItemCostAtSnap { get; set; }
        public int UsesLost { get; set; }
        public int? RemainingUsesBefore { get; set; }
        public int? RemainingUsesAfter { get; set; }
        public bool ItemRemoved { get; set; }
        public decimal GoldValueLost { get; set; }
    }
}