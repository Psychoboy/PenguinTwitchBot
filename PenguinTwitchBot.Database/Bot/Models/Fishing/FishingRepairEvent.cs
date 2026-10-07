using System.ComponentModel.DataAnnotations;

namespace PenguinTwitchBot.Database.Bot.Models.Fishing
{
    public class FishingRepairEvent
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(255)]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(255)]
        public string Username { get; set; } = string.Empty;

        public int ShopItemId { get; set; }

        public int UserBoostId { get; set; }

        [MaxLength(255)]
        public string ItemName { get; set; } = string.Empty;

        [MaxLength(32)]
        public string EquipmentSlot { get; set; } = string.Empty;

        public double DurabilityRestored { get; set; }

        public double MaxDurability { get; set; }

        public double DurabilityBefore { get; set; }

        public double DurabilityAfter { get; set; }

        public decimal GoldPaid { get; set; }

        public double RepairCostMultiplier { get; set; }

        [MaxLength(32)]
        public string RepairType { get; set; } = "Single";

        public DateTime RepairedAt { get; set; } = DateTime.UtcNow;
    }
}
