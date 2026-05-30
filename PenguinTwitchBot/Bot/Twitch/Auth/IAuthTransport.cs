namespace PenguinTwitchBot.Bot.Twitch.Auth;

public interface IAuthTransport
{
    Task<TwitchAuthTokenResponse?> ExchangeCodeAsync(string clientId, string clientSecret, string code, string redirectUri);
    Task<TwitchAuthenticatedUser?> GetAuthenticatedUserAsync(string clientId, string accessToken);
}
