using DotNetTwitchBot.Twitch.EventSub.Core.Models.Polls;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Poll Begin subscription type model
/// <para>Description:</para>
/// <para>A poll started on a specified channel.</para>
/// </summary>
public sealed class ChannelPollBegin : ChannelPollBase
{
    /// <summary>
    /// The time the poll will end.
    /// </summary>
    public DateTimeOffset EndsAt { get; set; } = DateTimeOffset.MinValue;
}