﻿using DotNetTwitchBot.Twitch.EventSub.Core.Models.Polls;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Poll End subscription type model
/// <para>Description:</para>
/// <para>A poll ended on a specified channel.</para>
/// </summary>
public sealed class ChannelPollEnd : ChannelPollBase
{
    /// <summary>
    /// The status of the poll. Valid values are completed, archived, and terminated.
    /// </summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>
    /// The time the poll ended.
    /// </summary>
    public DateTimeOffset EndedAt { get; set; } = DateTimeOffset.MinValue;
}