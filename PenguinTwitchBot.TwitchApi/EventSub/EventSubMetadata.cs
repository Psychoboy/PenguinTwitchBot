namespace PenguinTwitchBot.TwitchApi.EventSub;

/// <summary>
/// Metadata for EventSub notifications containing deduplication information.
/// </summary>
public abstract class EventSubMetadata
{
    /// <summary>
        /// An ID that uniquely identifies message. 
        /// </summary>
        public string MessageId { get; set; } = string.Empty;

        /// <summary>
        /// The type of notification.
        /// </summary>
        public string MessageType { get; set; } = string.Empty;

        /// <summary>
        /// The UTC date and time that Twitch sent the notification.
        /// </summary>
        public DateTime MessageTimestamp { get; set; }
}
