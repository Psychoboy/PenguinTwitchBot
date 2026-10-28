using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models.Giveaway
{
    public class GiveawayExclusion
    {
        [Key]
        public Guid Id { get; set; }
        public string Username { get; set; } = null!;
        public DateTime? ExpireDateTime { get; set; }
        public string? Reason { get; set; }
    }
}
