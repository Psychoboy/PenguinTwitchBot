namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Shoutouts;

/// <summary>
/// Defines the Shoutout data ChannelShoutoutCreate and ChannelShoutoutReceive share
/// </summary>
public abstract class ShoutoutBase
{
    /// <summary>
    /// An ID that identifies the broadcaster that sent the Shoutout.
    /// </summary>
    public string BroadcasterUserId { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster’s display name.
    /// </summary>
    public string BroadcasterUserName { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster’s login name.
    /// </summary>
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// The number of users that were watching the broadcaster’s stream at the time of the Shoutout.
    /// </summary>
    public int ViewerCount { get; set; }
    /// <summary>
    /// The UTC timestamp (in RFC3339 format) of when the moderator sent the Shoutout.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.MinValue;
}