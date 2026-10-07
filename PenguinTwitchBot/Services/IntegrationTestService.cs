using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;

namespace PenguinTwitchBot.Services;

public class IntegrationTestResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ErrorMessage { get; set; }
    public long LatencyMs { get; set; }
}

public interface IIntegrationTestService
{
    Task<IntegrationTestResult> TestYoutubeAsync(string? apiKey);
    Task<IntegrationTestResult> TestDiscordAsync(string? token);
    Task<IntegrationTestResult> TestWeatherAsync(string? apiKey, string? location);
    Task<IntegrationTestResult> TestOpenAiAsync(string? apiKey);
}

public class IntegrationTestService(IHttpClientFactory httpClientFactory) : IIntegrationTestService
{
    public async Task<IntegrationTestResult> TestYoutubeAsync(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new IntegrationTestResult
            {
                Success = false,
                StatusCode = (int)HttpStatusCode.BadRequest,
                Summary = "API Key Missing",
                ErrorMessage = "YouTube Data API v3 key is required."
            };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var ytService = new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = apiKey.Trim(),
                ApplicationName = "PenguinTwitchBot"
            });

            var request = ytService.Videos.List("snippet");
            request.Id = "dQw4w9WgXcQ"; // Public test video id (Rick Astley - Never Gonna Give You Up)
            request.MaxResults = 1;

            var response = await request.ExecuteAsync();
            sw.Stop();

            var videoTitle = response.Items?.FirstOrDefault()?.Snippet?.Title ?? "Public video query succeeded";

            return new IntegrationTestResult
            {
                Success = true,
                StatusCode = 200,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = "YouTube Data API Connected",
                Details = $"Successfully queried YouTube API. Test query returned: \"{videoTitle}\"."
            };
        }
        catch (GoogleApiException gEx)
        {
            sw.Stop();
            var status = (int)gEx.HttpStatusCode;
            var msg = gEx.Error?.Message ?? gEx.Message;
            return new IntegrationTestResult
            {
                Success = false,
                StatusCode = status,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = $"YouTube API Error ({status})",
                ErrorMessage = msg,
                Details = status switch
                {
                    400 => "Invalid API key format or parameters.",
                    403 => "Access forbidden. Ensure YouTube Data API v3 is enabled in Google Cloud Console and your quota limit is not exceeded.",
                    _ => gEx.Error?.Errors?.FirstOrDefault()?.Reason ?? "Unknown Google API error."
                }
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new IntegrationTestResult
            {
                Success = false,
                StatusCode = 500,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = "Connection Failed",
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<IntegrationTestResult> TestDiscordAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new IntegrationTestResult
            {
                Success = false,
                StatusCode = (int)HttpStatusCode.BadRequest,
                Summary = "Bot Token Missing",
                ErrorMessage = "Discord bot token is required."
            };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", token.Trim());
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("PenguinTwitchBot/1.0");

            var meResp = await client.GetAsync("https://discord.com/api/v10/users/@me");
            sw.Stop();

            if (!meResp.IsSuccessStatusCode)
            {
                var errorBody = await meResp.Content.ReadAsStringAsync();
                var status = (int)meResp.StatusCode;
                return new IntegrationTestResult
                {
                    Success = false,
                    StatusCode = status,
                    LatencyMs = sw.ElapsedMilliseconds,
                    Summary = $"Discord Auth Failed ({status})",
                    ErrorMessage = status == 401 ? "Unauthorized: Invalid bot token." : $"Discord API returned status {status}.",
                    Details = errorBody
                };
            }

            var meJson = await meResp.Content.ReadFromJsonAsync<DiscordMeResponse>();
            var botName = meJson?.Username ?? "Unknown Bot";

            // Also check guilds count
            int guildCount = 0;
            var guildsResp = await client.GetAsync("https://discord.com/api/v10/users/@me/guilds");
            if (guildsResp.IsSuccessStatusCode)
            {
                var guilds = await guildsResp.Content.ReadFromJsonAsync<List<DiscordGuildResponse>>() ?? [];
                guildCount = guilds.Count;
            }

            return new IntegrationTestResult
            {
                Success = true,
                StatusCode = 200,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = "Discord Bot Connected",
                Details = $"Authenticated as bot: \"{botName}\" (ID: {meJson?.Id}). Currently member in {guildCount} server(s)."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new IntegrationTestResult
            {
                Success = false,
                StatusCode = 500,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = "Connection Failed",
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<IntegrationTestResult> TestWeatherAsync(string? apiKey, string? location)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new IntegrationTestResult
            {
                Success = false,
                StatusCode = (int)HttpStatusCode.BadRequest,
                Summary = "API Key Missing",
                ErrorMessage = "WeatherAPI key is required."
            };
        }

        var loc = string.IsNullOrWhiteSpace(location) ? "London" : location.Trim();
        var sw = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient();
            var resp = await client.GetAsync($"https://api.weatherapi.com/v1/current.json?key={Uri.EscapeDataString(apiKey.Trim())}&q={Uri.EscapeDataString(loc)}&aqi=no");
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                var errorText = await resp.Content.ReadAsStringAsync();
                var status = (int)resp.StatusCode;
                string? specificError = null;
                try
                {
                    var errObj = JsonSerializer.Deserialize<WeatherErrorResponse>(errorText);
                    specificError = errObj?.Error?.Message;
                }
                catch { }

                return new IntegrationTestResult
                {
                    Success = false,
                    StatusCode = status,
                    LatencyMs = sw.ElapsedMilliseconds,
                    Summary = $"WeatherAPI Error ({status})",
                    ErrorMessage = specificError ?? $"WeatherAPI returned status {status}.",
                    Details = errorText
                };
            }

            var weather = await resp.Content.ReadFromJsonAsync<WeatherCurrentResponse>();
            var locName = weather?.Location?.Name ?? loc;
            var country = weather?.Location?.Country ?? "";
            var tempF = weather?.Current?.TempF ?? 0;
            var tempC = weather?.Current?.TempC ?? 0;
            var condition = weather?.Current?.Condition?.Text ?? "Unknown";

            return new IntegrationTestResult
            {
                Success = true,
                StatusCode = 200,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = "WeatherAPI Connected",
                Details = $"Location: {locName}, {country}. Current: {condition}, {tempF:F1}°F / {tempC:F1}°C (Humidity: {weather?.Current?.Humidity}%)."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new IntegrationTestResult
            {
                Success = false,
                StatusCode = 500,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = "Connection Failed",
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<IntegrationTestResult> TestOpenAiAsync(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new IntegrationTestResult
            {
                Success = false,
                StatusCode = (int)HttpStatusCode.BadRequest,
                Summary = "API Key Missing",
                ErrorMessage = "OpenAI API key is required."
            };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("PenguinTwitchBot/1.0");

            var resp = await client.GetAsync("https://api.openai.com/v1/models");
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                var errorBody = await resp.Content.ReadAsStringAsync();
                var status = (int)resp.StatusCode;
                string? specificError = null;
                try
                {
                    var errObj = JsonSerializer.Deserialize<OpenAiErrorResponse>(errorBody);
                    specificError = errObj?.Error?.Message;
                }
                catch { }

                return new IntegrationTestResult
                {
                    Success = false,
                    StatusCode = status,
                    LatencyMs = sw.ElapsedMilliseconds,
                    Summary = $"OpenAI Error ({status})",
                    ErrorMessage = specificError ?? (status == 401 ? "Unauthorized: Invalid OpenAI API key." : $"OpenAI returned status {status}."),
                    Details = errorBody
                };
            }

            var modelsJson = await resp.Content.ReadFromJsonAsync<OpenAiModelsResponse>();
            var modelCount = modelsJson?.Data?.Count ?? 0;

            return new IntegrationTestResult
            {
                Success = true,
                StatusCode = 200,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = "OpenAI API Connected",
                Details = $"Successfully authenticated. Account has access to {modelCount} models."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new IntegrationTestResult
            {
                Success = false,
                StatusCode = 500,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = "Connection Failed",
                ErrorMessage = ex.Message
            };
        }
    }

    private sealed record DiscordMeResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("username")] string? Username
    );

    private sealed record DiscordGuildResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name
    );

    private sealed record WeatherErrorResponse(
        [property: JsonPropertyName("error")] WeatherErrorDetail? Error
    );

    private sealed record WeatherErrorDetail(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] string? Message
    );

    private sealed record WeatherCurrentResponse(
        [property: JsonPropertyName("location")] WeatherLocationDetail? Location,
        [property: JsonPropertyName("current")] WeatherCurrentDetail? Current
    );

    private sealed record WeatherLocationDetail(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("country")] string? Country
    );

    private sealed record WeatherCurrentDetail(
        [property: JsonPropertyName("temp_f")] double TempF,
        [property: JsonPropertyName("temp_c")] double TempC,
        [property: JsonPropertyName("humidity")] int Humidity,
        [property: JsonPropertyName("condition")] WeatherConditionDetail? Condition
    );

    private sealed record WeatherConditionDetail(
        [property: JsonPropertyName("text")] string? Text
    );

    private sealed record OpenAiErrorResponse(
        [property: JsonPropertyName("error")] OpenAiErrorDetail? Error
    );

    private sealed record OpenAiErrorDetail(
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("code")] string? Code
    );

    private sealed record OpenAiModelsResponse(
        [property: JsonPropertyName("data")] List<OpenAiModelItem>? Data
    );

    private sealed record OpenAiModelItem(
        [property: JsonPropertyName("id")] string? Id
    );
}

