namespace DotNetTwitchBot.Bot.Events
{
    public class BanEventArgs
    {
        public string Name { get; set; } = null!;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string UserLogin { get; set; } = string.Empty;
        public string ModeratorUserId { get; set; } = string.Empty;
        public string ModeratorUserName { get; set; } = string.Empty;
        public string ModeratorLogin { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTimeOffset BannedAt { get; set; } = DateTimeOffset.MinValue;
        public bool IsPermanent { get; set; }
        public bool IsUnBan { get; set; }
        public DateTimeOffset? BanEndsAt { get; set; }
    }
}
