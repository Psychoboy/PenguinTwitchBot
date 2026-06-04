namespace PenguinTwitchBot.TwitchApi.EventSub.EventArgs;

/// <summary>
/// Generic EventSub notification payload wrapper.
/// Metadata is kept as TwitchLib type to preserve deduplication logic.
/// </summary>
public abstract class EventSubEventArgs<TEvent> : System.EventArgs
{
    /// <summary>
    /// Metadata for deduplication and event routing.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public EventSubMetadata Metadata { get; set; }

    /// <summary>
    /// The event-specific data payload.
    /// </summary>
    public TEvent Event { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}

/// <summary>
/// Typed EventSub notification payload with specific event data.
/// </summary>
public abstract class EventSubNotificationArgs<TEvent>: EventSubEventArgs <TEvent>
{
    
}
