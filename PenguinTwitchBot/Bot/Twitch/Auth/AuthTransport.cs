using PenguinTwitchBot.Bot.Twitch.Models.Auth;
using TwitchLib.Api;

namespace PenguinTwitchBot.Bot.Twitch.Auth;

public sealed class AuthTransport : IAuthTransport
{
    public async Task<TwitchAuthTokenResponse?> ExchangeCodeAsync(string clientId, string clientSecret, string code, string redirectUri)
    {
        var api = CreateApi(clientId);
        var response = await api.Auth.GetAccessTokenFromCodeAsync(code, clientSecret, redirectUri);
        if (response == null)
        {
            return null;
        }

        return new TwitchAuthTokenResponse
        {
            AccessToken = response.AccessToken,
            RefreshToken = response.RefreshToken,
            ExpiresIn = response.ExpiresIn
        };
    }

    public async Task<TwitchAuthenticatedUser?> GetAuthenticatedUserAsync(string clientId, string accessToken)
    {
        var api = CreateApi(clientId, accessToken);
        var users = await api.Helix.Users.GetUsersAsync();
        var user = users?.Users?.FirstOrDefault();
        if (user == null)
        {
            return null;
        }

        return new TwitchAuthenticatedUser
        {
            Id = user.Id,
            Login = user.Login,
            DisplayName = user.DisplayName,
            ProfileImageUrl = user.ProfileImageUrl
        };
    }

    public async Task<TokenValidation?> ValidateAccessTokenAsync(string accessToken)
    {
        var api = CreateApi(string.Empty, accessToken);
        var result = await api.Auth.ValidateAccessTokenAsync(accessToken);
        return result != null ? new TokenValidation(result.ExpiresIn) : null;
    }

    public async Task<TokenRefresh> RefreshAuthTokenAsync(string refreshToken, string clientSecret, string clientId)
    {
        var api = CreateApi(clientId);
        var result = await api.Auth.RefreshAuthTokenAsync(refreshToken, clientSecret, clientId);
        return new TokenRefresh(result.AccessToken, result.RefreshToken, result.ExpiresIn);
    }

    private static TwitchAPI CreateApi(string clientId, string? accessToken = null)
    {
        var api = new TwitchAPI();
        api.Settings.ClientId = clientId;
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            api.Settings.AccessToken = accessToken;
        }

        return api;
    }
}
