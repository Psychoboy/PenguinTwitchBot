namespace DotNetTwitchBot.Bot.Events
{
    public class BanEventArgs
    {
        public string Name { get; set; } = null!;
        public string UserId { get; set; } = string.Empty;
        public bool IsUnBan { get; set; }
        public DateTimeOffset? BanEndsAt { get; set; }
    }
}
