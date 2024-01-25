using DotNetTwitchBot.Twitch.EventSub.Core.Models.ChannelGoals;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Goal Progress subscription type model
/// <para>Description:</para>
/// <para>A channel goal progress changes, by either receiving a follow/unfollow or a subscription/unsubscription </para>
/// </summary>
public sealed class ChannelGoalProgress : ChannelGoalBase
{ }