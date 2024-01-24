using DotNetTwitchBot.Twitch.EventSub.Core.Models.Shoutouts;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Shoutout Receive subscription type model
/// <para>Description:</para>
/// <para>Defines the Shoutout data that you receive when the channel.shoutout.receive event occurs.</para>
/// </summary>
public sealed class ChannelShoutoutReceive : ShoutoutBase
{
    /// <summary>
    /// An ID that identifies the broadcaster that received the Shoutout.
    /// </summary>
    public string ToBroadcasterUserId { get; set; } = string.Empty;
    /// <summary>
    /// The receiving broadcaster’s display name.
    /// </summary>
    public string ToBroadcasterUserName { get; set; } = string.Empty;
    /// <summary>
    /// The receiving broadcaster’s login name.
    /// </summary>
    public string ToBroadcasterUserLogin { get; set; } = string.Empty;
}