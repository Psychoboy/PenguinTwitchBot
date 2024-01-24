using DotNetTwitchBot.Twitch.EventSub.Core.Models.Shoutouts;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Shoutout Create subscription type model
/// <para>Description:</para>
/// <para>Defines the Shoutout data that you receive when the channel.shoutout.create event occurs.</para>
/// </summary>
public sealed class ChannelShoutoutCreate : ShoutoutBase
{
    /// <summary>
    /// An ID that identifies the moderator that sent the Shoutout.
    /// <para>If the broadcaster sent the Shoutout, this ID is the same as the ID in broadcaster_user_id.</para>
    /// </summary>
    public string ModeratorUserId { get; set; } = string.Empty;
    /// <summary>
    /// The moderator’s login name.
    /// </summary>
    public string ModeratorUserName { get; set; } = string.Empty;
    /// <summary>
    /// The moderator’s display name.
    /// </summary>
    public string ModeratorUserLogin { get; set; } = string.Empty;
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
    /// <summary>
    /// The UTC timestamp (in RFC3339 format) of when the broadcaster may send a Shoutout to a different broadcaster.
    /// </summary>
    public DateTimeOffset CooldownEndsAt { get; set; } = DateTimeOffset.MinValue;
    /// <summary>
    /// The UTC timestamp (in RFC3339 format) of when the broadcaster may send another Shoutout to the broadcaster in to_broadcaster_user_id.
    /// </summary>
    public DateTimeOffset TargetCooldownEndsAt { get; set; } = DateTimeOffset.MinValue;
}