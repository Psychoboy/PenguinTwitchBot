namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models
{
    public class EventSubWebsocketSessionInfo
    {
        public string Id { get; set; } = default!;
        public string Status { get; set; } = default!;
        public string DisconnectReason { get; set; } = default!;
        public int? KeepaliveTimeoutSeconds { get; set; }
        public string ReconnectUrl { get; set; } = default!;
        public DateTime ConnectedAt { get; set; }
        public DateTime? DisconnectedAt { get; set; }
        public DateTime? ReconnectingAt { get; set; }
    }
}