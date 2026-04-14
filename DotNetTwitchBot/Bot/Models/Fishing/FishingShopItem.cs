using System.ComponentModel.DataAnnotations;

namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class FishingShopItem
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Cost { get; set; } = 100;
        public FishingBoostType BoostType { get; set; } = FishingBoostType.GeneralRarityBoost;
        public double BoostAmount { get; set; } = 0.05;
        public int? TargetFishTypeId { get; set; }
        public virtual FishType? TargetFishType { get; set; }
        public bool Enabled { get; set; } = true;

        // Equipment and usage properties
        public EquipmentSlot? EquipmentSlot { get; set; }
        public int? MaxUses { get; set; } // null = unlimited uses
        public bool IsConsumable { get; set; } = false; // If true, item is removed after uses expire
    }

    public enum FishingBoostType
    {
        GeneralRarityBoost,
        SpecificFishBoost,
        WeightBoost,
        StarBoost
    }

    public enum EquipmentSlot
    {
        Rod,        // Main fishing rod - General Rarity Boost
        Reel,       // Fishing reel - Star Boost (precision/control)
        Line,       // Fishing line - Weight Boost (strength)
        Hook,       // Fish hook - Star Boost (quality)
        Bait,       // Consumable bait - Specific Fish targeting
        Lure,       // Consumable lure - Specific Fish + General Rarity
        TackleBox,  // Permanent accessory - Multiple small boosts
        Net,        // Permanent accessory - Weight bonus
        Special     // Reserved for special event items
    }
}
