namespace PenguinTwitchBot.TwitchApi.EventSub;

/// <summary>
/// Metadata for EventSub notifications containing deduplication information.
/// </summary>
public class EventSubMetadata
{
    /// <summary>
    /// The unique message ID for deduplication.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// The type of EventSub event (e.g., "channel.chat.message").
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// The UTC timestamp when the event occurred.
    /// </summary>
    public DateTime MessageTimestamp { get; set; }
}
