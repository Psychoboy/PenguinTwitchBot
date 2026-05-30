namespace PenguinTwitchBot.Bot.Twitch.Models;

/// <summary>
/// Domain model for a Twitch clip
/// </summary>
public record Clip(
    string Id,
    string Url,
    string EmbedUrl,
    string Title,
    int ViewCount,
    DateTime CreatedAt,
    string Language,
    string ThumbnailUrl,
    string BroadcasterName,
    string BroadcasterId,
    string CreatorName,
    string CreatorId,
    float Duration,
    string? VideoId = null,
    string? GameId = null);
