namespace PenguinTwitchBot.TwitchApi.Models.Chat;

/// <summary>
/// Domain model for a chat badge set.
/// </summary>
public sealed record ChatBadgeSet(
    string SetId,
    IReadOnlyList<ChatBadgeVersion> Versions);

/// <summary>
/// Domain model for a chat badge version.
/// </summary>
public sealed record ChatBadgeVersion(
    string Id,
    string ImageUrl1x);
