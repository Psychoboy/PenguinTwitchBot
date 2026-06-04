namespace PenguinTwitchBot.TwitchApi.Models.Streams;

/// <summary>
/// Domain model for a live stream.
/// </summary>
public sealed record Stream(
    string Id,
    string UserId,
    string UserLogin,
    string UserName,
    string GameId,
    string GameName,
    string Type,
    string Title,
    IReadOnlyList<string> Tags,
    int ViewerCount,
    DateTime StartedAt,
    string Language,
    string ThumbnailUrl,
    bool IsMature);