namespace PenguinTwitchBot.TwitchApi.Models.ChannelPoints;

/// <summary>
/// Domain response model for channel point rewards.
/// </summary>
public sealed record GetChannelPointRewardsResponse(
    IReadOnlyList<ChannelPointReward> Data);