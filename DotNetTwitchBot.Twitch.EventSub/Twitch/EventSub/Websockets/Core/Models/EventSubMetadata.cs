namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models
{
    public class EventSubMetadata
    {
        public string MessageId { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public DateTime MessageTimestamp { get; set; }
        public string SubscriptionType { get; set; } = string.Empty;
        public string SubscriptionVersion { get; set; } = string.Empty;
    }
}