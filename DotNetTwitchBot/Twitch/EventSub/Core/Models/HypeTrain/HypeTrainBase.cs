﻿namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.HypeTrain;

/// <summary>
/// HypeTrain base class
/// </summary>
public class HypeTrainBase
{
    /// <summary>
    /// The requested broadcaster ID.
    /// </summary>
    public string BroadcasterUserId { get; set; } = string.Empty;
    /// <summary>
    /// The requested broadcaster display name.
    /// </summary>
    public string BroadcasterUserName { get; set; } = string.Empty;
    /// <summary>
    /// The requested broadcaster login.
    /// </summary>
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// Total points contributed to the Hype Train.
    /// </summary>
    public int Total { get; set; }
    /// <summary>
    /// The contributors with the most points contributed.
    /// </summary>
    public HypeTrainContribution[] TopContributions { get; set; } = Array.Empty<HypeTrainContribution>();
    /// <summary>
    /// The time when the Hype Train started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.MinValue;
    /// <summary>
    /// The current level of the Hype Train.
    /// </summary>
    public int Level { get; set; }
}