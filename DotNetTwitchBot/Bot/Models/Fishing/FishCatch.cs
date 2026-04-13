using System.ComponentModel.DataAnnotations;

namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class FishCatch
    {
        [Key]
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int FishTypeId { get; set; }
        public virtual FishType? FishType { get; set; }
        public int Stars { get; set; } = 1;
        public double Weight { get; set; } = 0;
        public int GoldEarned { get; set; } = 0;
        public DateTime CaughtAt { get; set; } = DateTime.UtcNow;
    }
}
