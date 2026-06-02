namespace PenguinTwitchBot.TwitchApi.Models.Channels;

/// <summary>
/// Domain response model for channel followers.
/// </summary>
public sealed record GetChannelFollowersResponse(
    IReadOnlyList<ChannelFollower> Data);