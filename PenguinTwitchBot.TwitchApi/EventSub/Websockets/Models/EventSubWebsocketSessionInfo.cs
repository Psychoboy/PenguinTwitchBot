namespace PenguinTwitchBot.TwitchApi.EventSub.Websockets.Models
{
    public class EventSubWebsocketSessionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DisconnectReason { get; set; } = string.Empty;
        public int? KeepaliveTimeoutSeconds { get; set; }
        public string? ReconnectUrl { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime? DisconnectedAt { get; set; }
        public DateTime? ReconnectingAt { get; set; }
    }
}