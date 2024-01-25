namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Chat Message Delete subscription type model
/// <para>Description:</para>
/// <para>A moderator removes a specific message.</para>
/// </summary>
public sealed class ChannelChatMessageDelete
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
    /// The ID of the user whose message was deleted.
    /// </summary>
    public string TargetUserId { get; set; } = string.Empty;
    /// <summary>
    /// The user name of the user whose message was deleted.
    /// </summary>
    public string TargetUserName { get; set; } = string.Empty;
    /// <summary>
    /// The user login of the user whose message was deleted.
    /// </summary>
    public string TargetUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// A UUID that identifies the message that was removed.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;
}