namespace PenguinTwitchBot.TwitchApi.EventSub.Models
{
    public class EventSubNotificationPayload<TEvent>
    {
        public required EventSubSubscription Subscription { get; set; }
        public required TEvent Event { get; set; }
    }
}