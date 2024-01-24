namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.GuestStar;

public abstract class ChannelGuestStarSessionBase
{
    /// <summary>
    /// The broadcaster user ID
    /// </summary>
    public string BroadcasterUserId { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster display name
    /// </summary>
    public string BroadcasterUserName { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster login
    /// </summary>
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// Unique ID representing the session.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
}