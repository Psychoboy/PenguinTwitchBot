namespace PenguinTwitchBot.TwitchApi.Models.Moderation;

/// <summary>
/// Domain response model for banned users.
/// </summary>
public sealed record GetBannedUsersResponse(
    IReadOnlyList<BannedUser> Data,
    string? Cursor);
