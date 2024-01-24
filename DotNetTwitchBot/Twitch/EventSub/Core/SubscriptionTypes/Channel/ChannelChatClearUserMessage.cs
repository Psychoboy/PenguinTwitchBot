namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Chat Clear User Message subscription type model
/// <para>Description:</para>
/// <para>A moderator or bot clears all messages for a specific user.</para>
/// </summary>
public sealed class ChannelChatClearUserMessage
{
    /// <summary>
    /// The broadcaster user ID.
    /// </summary>
    public string BroadcasterUserId { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster display name.
    /// </summary>
    public string BroadcasterUserName { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster login.
    /// </summary>
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// The ID of the user that was banned or put in a timeout. All of their messages are deleted.
    /// </summary>
    public string TargetUserId { get; set; } = string.Empty;
    /// <summary>
    /// The user name of the user that was banned or put in a timeout.
    /// </summary>
    public string TargetUserName { get; set; } = string.Empty;
    /// <summary>
    /// The user login of the user that was banned or put in a timeout.
    /// </summary>
    public string TargetUserLogin { get; set; } = string.Empty;
}