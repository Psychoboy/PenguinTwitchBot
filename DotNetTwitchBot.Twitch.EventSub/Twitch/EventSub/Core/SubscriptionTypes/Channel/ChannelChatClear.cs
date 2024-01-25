namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Chat Clear subscription type model
/// <para>Description:</para>
/// <para>A moderator or bot clears all messages from the chat room.</para>
/// </summary>
public sealed class ChannelChatClear
{
    /// <summary>
    /// The broadcaster user ID.
    /// </summary>
    public string BroadcasterUserId { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster login.
    /// </summary>
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster display name.
    /// </summary>
    public string BroadcasterUserName { get; set; } = string.Empty;
}