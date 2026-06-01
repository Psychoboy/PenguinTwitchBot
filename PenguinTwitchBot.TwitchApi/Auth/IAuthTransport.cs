using PenguinTwitchBot.Bot.Twitch.Models.Auth;

namespace PenguinTwitchBot.Bot.Twitch.Auth;

public interface IAuthTransport
{
    Task<TwitchAuthTokenResponse?> ExchangeCodeAsync(string clientId, string clientSecret, string code, string redirectUri);
    Task<TwitchAuthenticatedUser?> GetAuthenticatedUserAsync(string clientId, string accessToken);
    Task<TokenValidation?> ValidateAccessTokenAsync(string accessToken);
    Task<TokenRefresh> RefreshAuthTokenAsync(string refreshToken, string clientSecret, string clientId);
}
