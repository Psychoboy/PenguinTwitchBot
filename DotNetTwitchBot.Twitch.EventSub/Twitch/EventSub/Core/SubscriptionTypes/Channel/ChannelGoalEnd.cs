using DotNetTwitchBot.Twitch.EventSub.Core.Models.ChannelGoals;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Goal End subscription type model
/// <para>Description:</para>
/// <para>A channel goal ends</para>
/// </summary>
public sealed class ChannelGoalEnd : ChannelGoalBase
{
    /// <summary>
    /// The UTC timestamp in RFC 3339 format, which indicates when the broadcaster ended the goal.
    /// </summary>
    public DateTimeOffset EndedAt { get; set; } = DateTimeOffset.MinValue;
    /// <summary>
    /// A Boolean value that indicates whether the broadcaster achieved their goal. Is true if the goal was achieved; otherwise, false.
    /// </summary>
    public bool IsAchieved { get; set; }
}