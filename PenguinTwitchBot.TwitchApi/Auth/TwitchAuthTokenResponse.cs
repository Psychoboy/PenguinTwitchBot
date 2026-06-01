namespace PenguinTwitchBot.Bot.Twitch.Auth;

public sealed class TwitchAuthTokenResponse
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required int ExpiresIn { get; init; }
}
