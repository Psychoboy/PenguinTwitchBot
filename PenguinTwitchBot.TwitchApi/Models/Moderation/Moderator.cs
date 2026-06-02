namespace PenguinTwitchBot.TwitchApi.Models.Moderation;

/// <summary>
/// Domain model for a moderator.
/// </summary>
public record Moderator(
    string UserId,
    string UserLogin,
    string UserName);
