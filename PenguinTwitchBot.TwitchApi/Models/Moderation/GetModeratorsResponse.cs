namespace PenguinTwitchBot.TwitchApi.Models.Moderation;

/// <summary>
/// Domain response model for moderators.
/// </summary>
public sealed record GetModeratorsResponse(
    IReadOnlyList<Moderator> Data,
    string? Cursor);
