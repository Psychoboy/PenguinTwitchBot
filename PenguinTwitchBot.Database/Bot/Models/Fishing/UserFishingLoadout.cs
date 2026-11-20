using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PenguinTwitchBot.Database.Bot.Models.Fishing
{
    public class UserFishingLoadout
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid Id { get; set; } = Guid.CreateVersion7();

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(60)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// JSON array of saved slot entries (EquipmentSlot + UserFishingBoostId + ShopItemId fallback).
        /// </summary>
        [Required]
        public string SlotItemsJson { get; set; } = "[]";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class FishingLoadoutSlotEntry
    {
        public EquipmentSlot Slot { get; set; }
        public Guid UserBoostId { get; set; }
        public Guid ShopItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
    }

    public class FishingLoadoutApplyResult
    {
        public string LoadoutName { get; set; } = string.Empty;
        public int EquippedCount { get; set; }
        public List<string> EquippedItems { get; set; } = new();
        public List<string> MissingItems { get; set; } = new();
    }
}
