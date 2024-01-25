using DotNetTwitchBot.Twitch.EventSub.Core.Models.Predictions;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Prediction Begin subscription type model
/// <para>Description:</para>
/// <para>A Prediction started on a specified channel.</para>
/// </summary>
public sealed class ChannelPredictionBegin : ChannelPredictionBase
{
    /// <summary>
    /// The time the Channel Points Prediction will automatically lock.
    /// </summary>
    public DateTimeOffset LocksAt { get; set; } = DateTimeOffset.MinValue;
}