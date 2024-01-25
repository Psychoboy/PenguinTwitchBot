namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.ShieldMode;

/// <summary>
/// Defines the Shield Mode data that channel.shield_mode.begin and channel.shield_mode.end events share.
/// </summary>
public abstract class ShieldModeBase
{
    /// <summary>
    /// An ID that identifies the broadcaster whose Shield Mode status was updated.
    /// </summary>
    public string BroadcasterUserId { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster’s login name.
    /// </summary>
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster’s display name.
    /// </summary>
    public string BroadcasterUserName { get; set; } = string.Empty;
    /// <summary>
    /// An ID that identifies the moderator that updated the Shield Mode’s status.
    /// <para>If the broadcaster updated the status, this ID will be the same as broadcaster_user_id.</para>
    /// </summary>
    public string ModeratorUserId { get; set; } = string.Empty;
    /// <summary>
    /// The moderator’s login name.
    /// </summary>
    public string ModeratorUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// The moderator’s display name.
    /// </summary>
    public string ModeratorUserName { get; set; } = string.Empty;
}