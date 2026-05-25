using PenguinTwitchBot.Setup.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Setup.Controllers;

[ApiController]
public class AuthController(SetupService setupService, ILogger<AuthController> logger) : ControllerBase
{
    private const string StreamerRedirectUri = "http://localhost:5000/streamerredirect";
    private const string BotRedirectUri = "http://localhost:5000/botredirect";

    [HttpGet("/streamerredirect")]
    public async Task<IActionResult> StreamerRedirect(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            var safeError = error.Replace("\r", "").Replace("\n", "");
            logger.LogWarning("Streamer auth denied by user: {Error}", safeError);
            return Redirect("/?streamerAuthFailed=1");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect("/");

        if (!setupService.ValidateAndConsumeState(state))
        {
            logger.LogWarning("Invalid or expired OAuth state token in streamer redirect");
            return Redirect("/?streamerAuthFailed=1");
        }

        var clientId = setupService.PendingModel?.TwitchClientId ?? "";
        var clientSecret = setupService.PendingModel?.TwitchClientSecret ?? "";

        var tokens = await ExchangeCodeAsync(code, clientId, clientSecret, StreamerRedirectUri);
        if (tokens == null)
        {
            logger.LogError("Failed to exchange streamer auth code for tokens");
            return Redirect("/?streamerAuthFailed=1");
        }

        setupService.SetStreamerTokens(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn);
        logger.LogInformation("Streamer account authorized successfully");
        return Redirect("/?streamerAuthed=1");
    }

    [HttpGet("/botredirect")]
    public async Task<IActionResult> BotRedirect(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            var safeError = error.Replace("\r", "").Replace("\n", "");
            logger.LogWarning("Bot auth denied by user: {Error}", safeError);
            return Redirect("/?botAuthFailed=1");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect("/");

        if (!setupService.ValidateAndConsumeState(state))
        {
            logger.LogWarning("Invalid or expired OAuth state token in bot redirect");
            return Redirect("/?botAuthFailed=1");
        }

        var clientId = setupService.PendingModel?.TwitchBotClientId ?? setupService.PendingModel?.TwitchClientId ?? "";
        var clientSecret = setupService.PendingModel?.TwitchBotClientSecret ?? setupService.PendingModel?.TwitchClientSecret ?? "";

        var tokens = await ExchangeCodeAsync(code, clientId, clientSecret, BotRedirectUri);
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
