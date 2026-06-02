namespace PenguinTwitchBot.TwitchApi.Models.Auth;

/// <summary>
/// Domain model for a Twitch auth token refresh response.
/// </summary>
public record TokenRefresh(string AccessToken, string RefreshToken, int ExpiresIn);
