namespace PenguinTwitchBot.TwitchApi.EventSub.Models
{
    public class EventSubSubscription
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, string> Condition { get; set; } = new();
        public EventSubTransport Transport { get; set; } = new();
        public bool IsBatchingEnabled { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int Cost { get; set; }
    }
}