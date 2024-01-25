using DotNetTwitchBot.Twitch.EventSub.Core.Models.GuestStar;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel GuestStar Session Begin subscription type model
/// <para>Description:</para>
/// <para>The channel.guest_star_session.begin subscription type sends a notification when the host begins a new Guest Star session.</para>
/// </summary>
public sealed class ChannelGuestStarSessionBegin : ChannelGuestStarSessionBase
{
    /// <summary>
    /// RFC3339 timestamp indicating the time the session began.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }
}