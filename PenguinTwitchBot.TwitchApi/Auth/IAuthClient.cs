using PenguinTwitchBot.TwitchApi.Models.Auth;

namespace PenguinTwitchBot.TwitchApi.Auth;

public interface IAuthClient
{
    Task<TwitchAuthTokenResponse?> ExchangeCodeAsync(string clientId, string clientSecret, string code, string redirectUri);
    Task<TwitchAuthenticatedUser?> GetAuthenticatedUserAsync(string clientId, string accessToken);
    Task<TokenValidation?> ValidateAccessTokenAsync(string accessToken);
    Task<TokenRefresh> RefreshAuthTokenAsync(string refreshToken, string clientSecret, string clientId);
}
