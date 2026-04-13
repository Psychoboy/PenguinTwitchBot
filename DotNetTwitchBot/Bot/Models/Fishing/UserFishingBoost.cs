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
    }
}
