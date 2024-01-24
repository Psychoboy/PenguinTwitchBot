namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models
{
    public class EventSubWebsocketSessionInfoMessage
    {
        public EventSubMetadata Metadata { get; set; } = default!;
        public EventSubWebsocketSessionInfoPayload Payload { get; set; } = default!;
    }
}