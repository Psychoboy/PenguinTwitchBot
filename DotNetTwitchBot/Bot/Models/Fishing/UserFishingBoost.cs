using System.ComponentModel.DataAnnotations;

namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class UserFishingBoost
    {
        [Key]
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int ShopItemId { get; set; }
        public virtual FishingShopItem? ShopItem { get; set; }
        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

        // Equipment and usage tracking
        public bool IsEquipped { get; set; } = false;
        public int RemainingUses { get; set; } = 0; // -1 = unlimited
        public DateTime? LastUsedAt { get; set; }
    }
}
