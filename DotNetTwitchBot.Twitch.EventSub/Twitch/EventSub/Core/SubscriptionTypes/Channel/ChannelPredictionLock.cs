using DotNetTwitchBot.Twitch.EventSub.Core.Models.Predictions;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Prediction Lock subscription type model
/// <para>Description:</para>
/// <para>A Prediction was locked on a specified channel.</para>
/// </summary>
public sealed class ChannelPredictionLock : ChannelPredictionBase
{
    /// <summary>
    /// The time the Channel Points Prediction was locked.
    /// </summary>
    public DateTimeOffset LockedAt { get; set; } = DateTimeOffset.MinValue;
}