namespace PenguinTwitchBot.TwitchApi.Models.Moderation;

/// <summary>
/// Domain model for a banned or timed-out viewer.
/// </summary>
public record BannedUser(
    string UserId,
    string UserLogin,
    DateTime? ExpiresAt);
