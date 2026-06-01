namespace PenguinTwitchBot.Bot.Twitch.Models;

/// <summary>
/// Domain model for a Twitch access token validation response.
/// </summary>
public record TokenValidation(int ExpiresIn);
