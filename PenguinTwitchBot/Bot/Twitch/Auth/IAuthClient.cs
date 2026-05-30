namespace PenguinTwitchBot.Bot.Twitch.Auth;

public interface IAuthClient
{
    Task<TwitchAuthTokenResponse?> ExchangeCodeAsync(string clientId, string clientSecret, string code, string redirectUri);
    Task<TwitchAuthenticatedUser?> GetAuthenticatedUserAsync(string clientId, string accessToken);
}
