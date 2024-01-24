using DotNetTwitchBot.Twitch.EventSub.Core.Models.Predictions;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Prediction End subscription type model
/// <para>Description:</para>
/// <para>A Prediction ended on a specified channel.</para>
/// </summary>
public sealed class ChannelPredictionEnd : ChannelPredictionBase
{
    /// <summary>
    /// ID of the winning outcome.
    /// </summary>
    public string WinningOutcomeId { get; set; } = string.Empty;
    /// <summary>
    /// The status of the Channel Points Prediction. Valid values are resolved and canceled.
    /// </summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>
    /// The time the Channel Points Prediction ended.
    /// </summary>
    public DateTimeOffset EndedAt { get; set; } = DateTimeOffset.MinValue;
}