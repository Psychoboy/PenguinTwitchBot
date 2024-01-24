namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models
{
    public class EventSubNotification<T>
    {
        public EventSubMetadata Metadata { get; set; } = default!;
        public EventSubNotificationPayload<T> Payload { get; set; } = default!;
    }
}