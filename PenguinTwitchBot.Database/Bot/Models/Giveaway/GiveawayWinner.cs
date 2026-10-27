using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models.Giveaway
{
    public class GiveawayWinner
    {
        [Key]
        public Guid Id { get; set; }
        public string Username { get; set; } = "";
        public DateTime WinningDate { get; set; } = DateTime.UtcNow;
        public string Prize { get; set; } = "";
        public string PrizeTier { get; set; } = "";
        public bool IsFollowing { get; set; }
        public string Email { get; set; } = "";
        public string ClaimedBy { get; set; } = "";
    }
}