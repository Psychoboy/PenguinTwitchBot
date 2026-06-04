using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using PenguinTwitchBot.Setup.Models;

namespace PenguinTwitchBot.Setup.Services
{
    public class SetupService(
        string secretsFilePath,
        IHostApplicationLifetime lifetime,
        ILogger<SetupService> logger)
    {
        private readonly ConcurrentDictionary<string, byte> _pendingStates = new();

        public string SecretsFilePath => secretsFilePath;

        // -- Pending model (survives OAuth browser redirect) ---------------
        public SetupWizardModel? PendingModel { get; private set; }
        public int PendingStep { get; private set; }

        public void SavePendingModel(SetupWizardModel model, int step)
        {
            PendingModel = model;
            PendingStep = step;
        }

        // -- OAuth state tokens --------------------------------------------
        public string BeginAuth()
        {
            var state = Guid.NewGuid().ToString("N");
            _pendingStates[state] = 0;
            return state;
        }

        public bool ValidateAndConsumeState(string state)
            => _pendingStates.TryRemove(state, out _);

        // -- Stored tokens -------------------------------------------------
        public string? TwitchAccessToken { get; private set; }
        public string? TwitchRefreshToken { get; private set; }
        public int TwitchExpiresIn { get; private set; }
        public bool StreamerAuthed => !string.IsNullOrEmpty(TwitchAccessToken);

        public string? TwitchBotAccessToken { get; private set; }
        public string? TwitchBotRefreshToken { get; private set; }
        public int TwitchBotExpiresIn { get; private set; }
        public bool BotAuthed => !string.IsNullOrEmpty(TwitchBotAccessToken);

        public void SetStreamerTokens(string accessToken, string refreshToken, int expiresIn)
        {
            TwitchAccessToken = accessToken;
            TwitchRefreshToken = refreshToken;
            TwitchExpiresIn = expiresIn;
        }

        public void SetBotTokens(string accessToken, string refreshToken, int expiresIn)
        {
            TwitchBotAccessToken = accessToken;
            TwitchBotRefreshToken = refreshToken;
            TwitchBotExpiresIn = expiresIn;
        }

        public async Task<bool> RefreshBotAccessTokenAsync(string clientId, string clientSecret)
        {
            using var http = new HttpClient();
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "client_credentials"
            });

            try
            {
                var response = await http.PostAsync("https://id.twitch.tv/oauth2/token", form);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Failed to refresh bot access token during setup. Status code: {StatusCode}", response.StatusCode);
                    return false;
                }

                var json = await response.Content.ReadAsStringAsync();
                var tokens = JsonSerializer.Deserialize<TwitchClientCredentialsTokenResult>(json);
                if (tokens == null || string.IsNullOrWhiteSpace(tokens.AccessToken))
                {
                    logger.LogWarning("Failed to deserialize refreshed bot access token during setup");
                    return false;
                }

                TwitchBotAccessToken = tokens.AccessToken;
                TwitchBotRefreshToken = "";
                TwitchBotExpiresIn = tokens.ExpiresIn;
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error refreshing bot access token during setup");
                return false;
            }
        }

        public async Task SaveSetupAsync(SetupWizardModel model)
        {
            var webauth = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            var providerString = model.DatabaseProvider switch
            {
                DatabaseProviderOption.Postgres => "postgres",
                _ => "sqlite"
            };

            var secrets = new Dictionary<string, object?>
            {
                ["botName"] = model.BotName.Trim(),
                ["broadcaster"] = model.Broadcaster.Trim(),
                ["twitchClientId"] = model.TwitchClientId.Trim(),
                ["twitchClientSecret"] = model.TwitchClientSecret.Trim(),
                ["twitchAccessToken"] = TwitchAccessToken ?? "",
                ["twitchRefreshToken"] = TwitchRefreshToken ?? "",
                ["twitchBotClientId"] = model.TwitchBotClientId.Trim(),
                ["twitchBotClientSecret"] = model.TwitchBotClientSecret.Trim(),
                ["twitchBotAccessToken"] = TwitchBotAccessToken ?? "",
                ["twitchBotRefreshToken"] = TwitchBotRefreshToken ?? "",
                ["botExpiresIn"] = (object)TwitchBotExpiresIn,
                ["expiresIn"] = (object)TwitchExpiresIn,
                ["youtubeApi"] = model.YoutubeApiKey.Trim(),
                ["webauth"] = webauth,
                ["Database"] = new Dictionary<string, object?>
                {
                    ["Provider"] = providerString
                },
                ["ConnectionStrings"] = new Dictionary<string, object?>
                {
                    ["SqliteConnection"] = ToSqliteConnectionString(model.SqliteFilePath.Trim()),
                    ["PostgresConnection"] = model.PostgresConnectionString.Trim()
                },
                ["Discord"] = new Dictionary<string, object?>
                {
                    ["discordToken"] = model.DiscordEnabled ? model.DiscordToken.Trim() : "",
                    ["DiscordServerId"] = (object)ParseUlong(model.DiscordServerId),
                    ["BroadcastChannel"] = (object)ParseUlong(model.DiscordBroadcastChannel),
                    ["PingRoleWhenLive"] = (object)ParseUlong(model.DiscordPingRoleId),
                    ["RoleIdToAssignMemberWhenLive"] = (object)ParseUlong(model.DiscordMemberRoleId)
                },
                ["Weather"] = new Dictionary<string, object?>
                {
                    ["ApiKey"] = model.WeatherEnabled ? model.WeatherApiKey.Trim() : "",
                    ["DefaultLocation"] = model.WeatherEnabled ? model.WeatherDefaultLocation.Trim() : ""
                },
                ["OpenAI"] = new Dictionary<string, object?>
                {
                    ["ApiKey"] = model.OpenAiEnabled ? model.OpenAiApiKey.Trim() : ""
                }
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(secretsFilePath)) ?? ".");
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(secrets, options);
            await File.WriteAllTextAsync(secretsFilePath, json);
            logger.LogInformation("Setup complete. Configuration saved to {Path}", secretsFilePath);
        }

        public void SignalStop() => lifetime.StopApplication();

        public SetupWizardModel? TryLoadExistingConfig()
        {
            try
            {
                if (!File.Exists(secretsFilePath)) return null;
                var json = File.ReadAllText(secretsFilePath);
                var node = JsonNode.Parse(json);
                if (node == null) return null;

                var model = new SetupWizardModel
                {
                    BotName = node["botName"]?.GetValue<string>() ?? "",
                    Broadcaster = node["broadcaster"]?.GetValue<string>() ?? "",
                    TwitchClientId = node["twitchClientId"]?.GetValue<string>() ?? "",
                    TwitchClientSecret = node["twitchClientSecret"]?.GetValue<string>() ?? "",
                    TwitchBotClientId = node["twitchBotClientId"]?.GetValue<string>() ?? "",
                    TwitchBotClientSecret = node["twitchBotClientSecret"]?.GetValue<string>() ?? "",
                    YoutubeApiKey = node["youtubeApi"]?.GetValue<string>() ?? "",
                    SqliteFilePath = node["ConnectionStrings"]?["SqliteConnection"]?.GetValue<string>() ?? "",
                    PostgresConnectionString = node["ConnectionStrings"]?["PostgresConnection"]?.GetValue<string>() ?? "",
                    DiscordToken = node["Discord"]?["discordToken"]?.GetValue<string>() ?? "",
                    DiscordServerId = node["Discord"]?["DiscordServerId"]?.ToString() ?? "",
                    DiscordBroadcastChannel = node["Discord"]?["BroadcastChannel"]?.ToString() ?? "",
                    DiscordPingRoleId = node["Discord"]?["PingRoleWhenLive"]?.ToString() ?? "",
                    DiscordMemberRoleId = node["Discord"]?["RoleIdToAssignMemberWhenLive"]?.ToString() ?? "",
                    WeatherApiKey = node["Weather"]?["ApiKey"]?.GetValue<string>() ?? "",
                    WeatherDefaultLocation = node["Weather"]?["DefaultLocation"]?.GetValue<string>() ?? "",
                    OpenAiApiKey = node["OpenAI"]?["ApiKey"]?.GetValue<string>() ?? "",
                };

                var providerStr = node["Database"]?["Provider"]?.GetValue<string>() ?? "sqlite";
                model.DatabaseProvider = providerStr switch
                {
                    "postgres" => DatabaseProviderOption.Postgres,
                    _ => DatabaseProviderOption.Sqlite
                };

                model.BotUseSameApp = model.TwitchBotClientId == model.TwitchClientId &&
                                      model.TwitchBotClientSecret == model.TwitchClientSecret &&
                                      !string.IsNullOrEmpty(model.TwitchClientId);

                model.DiscordEnabled = !string.IsNullOrEmpty(model.DiscordToken);
                model.WeatherEnabled = !string.IsNullOrEmpty(model.WeatherApiKey);
                model.OpenAiEnabled = !string.IsNullOrEmpty(model.OpenAiApiKey);

                logger.LogInformation("Loaded existing configuration from {Path}", secretsFilePath);
                return model;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not load existing config from {Path}", secretsFilePath);
                return null;
            }
        }

        private static string ToSqliteConnectionString(string path) =>
            string.IsNullOrEmpty(path) ? "" :
            path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ? path :
            $"Data Source={path}";

        private static ulong ParseUlong(string? value) =>
            ulong.TryParse(value?.Trim(), out var result) ? result : 0;

        private sealed record TwitchClientCredentialsTokenResult(
            [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
            [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);
    }
}
