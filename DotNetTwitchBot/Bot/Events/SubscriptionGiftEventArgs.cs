namespace DotNetTwitchBot.Bot.Events
{
    public class SubscriptionGiftEventArgs
    {
        public string? Name { get; set; } = null!;
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = null!;
        public int GiftAmount { get; set; }
        public int? TotalGifted { get; set; }
    }
}