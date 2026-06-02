namespace PenguinTwitchBot.TwitchApi.Models.Channels;

/// <summary>
/// Domain model for a Twitch channel follower.
/// </summary>
public sealed record ChannelFollower(
    string UserId,
    string UserLogin,
    string UserName,
    string FollowedAt);