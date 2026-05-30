namespace PenguinTwitchBot.Bot.Twitch.Models;

/// <summary>
/// Domain model for a Twitch user, replacing direct TwitchLib.Api types
/// </summary>
public record User(
    string Id,
    string Login,
    string DisplayName,
    string Description,
    DateTime CreatedAt,
    string? ProfileImageUrl = null,
    string? OfflineImageUrl = null,
    string? Email = null,
    string? Type = null);
