using DotNetTwitchBot.Twitch.EventSub.Core.Models.GuestStar;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel GuestStar Session End subscription type model
/// <para>Description:</para>
/// <para>The channel.guest_star_session.end subscription type sends a notification when a running Guest Star session is ended by the host, or automatically by the system.</para>
/// </summary>
public sealed class ChannelGuestStarSessionEnd : ChannelGuestStarSessionBase
{
    /// <summary>
    /// RFC3339 timestamp indicating the time the session began.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }
    /// <summary>
    /// RFC3339 timestamp indicating the time the session ended.
    /// </summary>
    public DateTimeOffset EndedAt { get; set; }
}