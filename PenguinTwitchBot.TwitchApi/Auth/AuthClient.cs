using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.Auth;

namespace PenguinTwitchBot.TwitchApi.Auth;

public sealed class AuthClient(ILogger<AuthClient> logger, IAuthTransport transport) : TwitchClientRetryBase(logger), IAuthClient
{
    public Task<TwitchAuthTokenResponse?> ExchangeCodeAsync(string clientId, string clientSecret, string code, string redirectUri)
    {
        return ExecuteWithRetryAsync(() => transport.ExchangeCodeAsync(clientId, clientSecret, code, redirectUri), "exchange auth code");
    }

    public Task<TwitchAuthenticatedUser?> GetAuthenticatedUserAsync(string clientId, string accessToken)
    {
        return ExecuteWithRetryAsync(() => transport.GetAuthenticatedUserAsync(clientId, accessToken), "load authenticated user");
    }

    public Task<TokenValidation?> ValidateAccessTokenAsync(string accessToken)
    {
        return ExecuteWithRetryAsync(() => transport.ValidateAccessTokenAsync(accessToken), "validate access token");
    }

    public Task<TokenRefresh> RefreshAuthTokenAsync(string refreshToken, string clientSecret, string clientId)
    {
        return ExecuteWithRetryAsync(() => transport.RefreshAuthTokenAsync(refreshToken, clientSecret, clientId), "refresh auth token");
    }
}
