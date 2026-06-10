using PenguinTwitchBot.Setup.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Setup.Controllers;

[ApiController]
public class AuthController(SetupService setupService, ILogger<AuthController> logger) : ControllerBase
{
    [HttpGet("/redirect")]
    public async Task<IActionResult> OAuthRedirect(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            var safeError = error.Replace("\r", "").Replace("\n", "");
            logger.LogWarning("OAuth authorization denied: {Error}", safeError);
            var failParam = setupService.PendingStep == 4 ? "botAuthFailed" : "streamerAuthFailed";
            return Redirect($"/?{failParam}=1");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect("/");

        var intent = setupService.ValidateAndConsumeState(state);
        if (intent is null)
        {
            logger.LogWarning("Invalid or expired OAuth state token");
            // PendingStep tells us which step to return to
            var failParam = setupService.PendingStep == 4 ? "botAuthFailed" : "streamerAuthFailed";
            return Redirect($"/?{failParam}=1");
        }

        var redirectUri = $"{GetRequestOrigin()}/redirect";

        return intent switch
        {
            OAuthIntent.Streamer => await HandleStreamerCallback(code, redirectUri),
            OAuthIntent.Bot => await HandleBotCallback(code, redirectUri),
            _ => Redirect("/")
        };
    }

    private async Task<IActionResult> HandleStreamerCallback(string code, string redirectUri)
    {
        var clientId = setupService.PendingModel?.TwitchClientId ?? "";
        var clientSecret = setupService.PendingModel?.TwitchClientSecret ?? "";

        var tokens = await ExchangeCodeAsync(code, clientId, clientSecret, redirectUri);
        if (tokens == null)
        {
            logger.LogError("Failed to exchange streamer auth code for tokens");
            return Redirect("/?streamerAuthFailed=1");
        }

        setupService.SetStreamerTokens(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn);
        logger.LogInformation("Streamer account authorized successfully");
        return Redirect("/?streamerAuthed=1");
    }

    private async Task<IActionResult> HandleBotCallback(string code, string redirectUri)
    {
        var clientId = setupService.PendingModel?.TwitchClientId ?? "";
        var clientSecret = setupService.PendingModel?.TwitchClientSecret ?? "";

        var tokens = await ExchangeCodeAsync(code, clientId, clientSecret, redirectUri);
        if (tokens == null)
        {
            logger.LogError("Failed to exchange bot auth code for tokens");
            return Redirect("/?botAuthFailed=1");
        }

        setupService.SetBotTokens(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn);
        var refreshed = await setupService.RefreshBotAccessTokenAsync(clientId, clientSecret);
        if (!refreshed)
        {
            logger.LogError("Failed to refresh bot token after OAuth completion");
            return Redirect("/?botAuthFailed=1");
        }

        logger.LogInformation("Bot account authorized successfully");
        return Redirect("/?botAuthed=1");
    }

    private string GetRequestOrigin()
    {
        var request = HttpContext.Request;
        var origin = $"{request.Scheme}://{request.Host.Value}";
        if (request.PathBase.HasValue)
            origin += request.PathBase.Value;
        return origin.TrimEnd('/');
    }

    private static async Task<TwitchTokenResult?> ExchangeCodeAsync(
        string code, string clientId, string clientSecret, string redirectUri)
    {
        using var http = new HttpClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        });

        try
        {
            var response = await http.PostAsync("https://id.twitch.tv/oauth2/token", form);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TwitchTokenResult>(json);
        }
        catch
        {
            return null;
        }
    }

    private sealed record TwitchTokenResult(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
