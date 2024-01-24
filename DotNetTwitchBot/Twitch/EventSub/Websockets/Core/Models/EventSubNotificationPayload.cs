namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models
{
    public class EventSubNotificationPayload<T>
    {
        public EventSubTransport Transport { get; set; } = default!;
        public T Event { get; set; } = default!;
    }
}