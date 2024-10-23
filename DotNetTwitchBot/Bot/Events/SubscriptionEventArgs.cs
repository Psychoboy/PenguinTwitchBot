namespace DotNetTwitchBot.Bot.Events
{
    public class SubscriptionEventArgs
    {
        public string Name { get; set; } = null!;
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = null!;
        public int? Count { get; set; } = null;
        public int? Streak { get; set; } = null!;
        public string Tier { get; set; } = null!;
        public bool IsGift { get; set; } = false;
        public bool IsRenewal { get; set; } = false;
        public string? Message { get; set; }
    }
}