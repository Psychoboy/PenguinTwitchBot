using PenguinTwitchBot.TwitchApi.Models.Auth;
using PenguinTwitchBot.TwitchApi.Helix;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.TwitchApi.Auth;

public sealed class AuthTransport : IAuthTransport
{
    internal const string TwitchIdClientName = "TwitchId";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public AuthTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<TwitchAuthTokenResponse?> ExchangeCodeAsync(string clientId, string clientSecret, string code, string redirectUri)
    {
        using var http = _httpClientFactory.CreateClient(TwitchIdClientName);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
        });
        using var response = await http.PostAsync("oauth2/token", content);
        response.EnsureSuccessStatusCode();

        var payload = await DeserializeAsync<TwitchTokenApiResponse>(response);
        var token = payload;
        if (token == null)
        {
            return null;
        }

        return new TwitchAuthTokenResponse
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresIn = token.ExpiresIn
        };
    }

    public async Task<TwitchAuthenticatedUser?> GetAuthenticatedUserAsync(string clientId, string accessToken)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        using var response = await http.GetAsync("users");
        response.EnsureSuccessStatusCode();

        var payload = await DeserializeAsync<HelixUsersResponse>(response);
        var user = payload?.Data.FirstOrDefault();
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
        using var http = _httpClientFactory.CreateClient(TwitchIdClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, "oauth2/validate");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request);
        if(response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

        var result = await DeserializeAsync<ValidateAccessTokenApiResponse>(response);
        return result != null ? new TokenValidation(result.ExpiresIn) : null;
    }

    public async Task<TokenRefresh> RefreshAuthTokenAsync(string refreshToken, string clientSecret, string clientId)
    {
        using var http = _httpClientFactory.CreateClient(TwitchIdClientName);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        });
        using var response = await http.PostAsync("oauth2/token", content);
        response.EnsureSuccessStatusCode();

        var result = await DeserializeAsync<TwitchTokenApiResponse>(response)
            ?? throw new InvalidOperationException("Token refresh response was empty.");
        return new TokenRefresh(result.AccessToken, result.RefreshToken, result.ExpiresIn);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        if (stream.CanSeek && stream.Length == 0)
        {
            return default;
        }

        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }

    private sealed record TwitchTokenApiResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record ValidateAccessTokenApiResponse(
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record HelixUsersResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<HelixUserItem> Data);

    private sealed record HelixUserItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("profile_image_url")] string ProfileImageUrl);
}
