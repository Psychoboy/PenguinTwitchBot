namespace PenguinTwitchBot.Database.Bot.Models
{
    [IndexAttribute(nameof(Username), IsUnique = false)]
    public class ViewerChatHistory
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DisplayName { get; set; } = default!;
        public string Username { get; set; } = default!;
        public string Message { get; set; } = default!;
        public string? MessageId { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
