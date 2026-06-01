namespace PenguinTwitchBot.Bot.Twitch.Models;

/// <summary>
/// Domain model for a Twitch auth token refresh response.
/// </summary>
public record TokenRefresh(string AccessToken, string RefreshToken, int ExpiresIn);
