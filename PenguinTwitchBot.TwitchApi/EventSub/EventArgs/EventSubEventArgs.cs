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
    public required EventSubMetadata Metadata { get; set; }

    /// <summary>
    /// The event-specific data payload.
    /// </summary>
    public required TEvent Event { get; set; }
}

/// <summary>
/// Typed EventSub notification payload with specific event data.
/// </summary>
public abstract class EventSubNotificationArgs<TEvent>: EventSubEventArgs <TEvent>
{
    
}
