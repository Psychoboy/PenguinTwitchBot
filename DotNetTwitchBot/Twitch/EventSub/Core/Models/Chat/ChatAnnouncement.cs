namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Information about the announcement event. Null if notice_type is not announcement
/// </summary>
public sealed class ChatAnnouncement
{
    /// <summary>
    /// Color of the announcement.
    /// </summary>
    public string Color { get; set; } = string.Empty;
}