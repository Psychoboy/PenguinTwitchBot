using System.ComponentModel.DataAnnotations;

namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class FishingGold
    {
        [Key]
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int TotalGold { get; set; } = 0;
    }
}
