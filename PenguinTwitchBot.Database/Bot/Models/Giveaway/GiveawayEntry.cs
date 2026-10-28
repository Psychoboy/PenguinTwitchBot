using Newtonsoft.Json;

namespace PenguinTwitchBot.Database.Bot.Models.Giveaway
{
    [IndexAttribute(nameof(Username), IsUnique = true)]
    public class GiveawayEntry
    {
        [Key]
        public Guid Id { get; set; }
        public string Username { get; set; } = "";
        public string UserId { get; set; } = string.Empty;
        public Int32 Tickets { get; set; } = 0;
    }
}