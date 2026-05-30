using PenguinTwitchBot.Bot.Twitch.Helix;

namespace PenguinTwitchBot.Bot.Twitch.Auth;

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
}
