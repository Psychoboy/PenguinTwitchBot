using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models
{
    [IndexAttribute(nameof(UserId))]
    [IndexAttribute(nameof(Username))]
    [IndexAttribute(nameof(Time))]
    public class ViewerTime
    {
        [Key]
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        [MaxLength(255)]
        public string Username { get; set; } = "";
        public long Time { get; set; } = 0;
        public bool banned { get; set; } = false;
    }
}