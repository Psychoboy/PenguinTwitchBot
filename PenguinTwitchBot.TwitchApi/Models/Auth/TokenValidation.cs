namespace PenguinTwitchBot.TwitchApi.Models.Auth;

/// <summary>
/// Domain model for a Twitch access token validation response.
/// </summary>
public record TokenValidation(int ExpiresIn);
