namespace PenguinTwitchBot.TwitchApi.EventSub.Models
{
    public class EventSubNotificationPayload<TEvent>
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public EventSubSubscription Subscription { get; set; }
        public TEvent Event { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    }
}