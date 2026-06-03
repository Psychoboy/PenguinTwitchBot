namespace PenguinTwitchBot.TwitchApi.EventSub.EventArgs;

/// <summary>
/// Generic EventSub notification payload wrapper.
/// Metadata is kept as TwitchLib type to preserve deduplication logic.
/// </summary>
public abstract class EventSubEventArgs
{
    /// <summary>
    /// Metadata for deduplication and event routing.
    /// </summary>
    public EventSubMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Typed EventSub notification payload with specific event data.
/// </summary>
public abstract class EventSubEventArgs<TEvent>: EventSubEventArgs
    where TEvent : notnull
{
    /// <summary>
    /// The event-specific data payload.
    /// </summary>
    public TEvent Event { get; set; } = default!;
}
