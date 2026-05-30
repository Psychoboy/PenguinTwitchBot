namespace PenguinTwitchBot.Bot.Twitch.Models;

/// <summary>
/// Domain model for Twitch game information
/// </summary>
public record Game(
    string Id,
    string Name,
    string? BoxArtUrl = null);
