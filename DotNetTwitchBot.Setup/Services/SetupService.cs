using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using DotNetTwitchBot.Setup.Models;

namespace DotNetTwitchBot.Setup.Services
{
    public class SetupService(
        string secretsFilePath,
        IHostApplicationLifetime lifetime,
        ILogger<SetupService> logger)
    {
        private readonly ConcurrentDictionary<string, byte> _pendingStates = new();

        public string SecretsFilePath => secretsFilePath;

        // ── Pending model (survives OAuth browser redirect) ───────────────
        public SetupWizardModel? PendingModel { get; private set; }
        public int PendingStep { get; private set; }

        public void SavePendingModel(SetupWizardModel model, int step)
        {
            PendingModel = model;
            PendingStep = step;
        }

        // ── OAuth state tokens ────────────────────────────────────────────
        public string BeginAuth()
        {
            var state = Guid.NewGuid().ToString("N");
            _pendingStates[state] = 0;
            return state;
        }

        public bool ValidateAndConsumeState(string state)
            => _pendingStates.TryRemove(state, out _);

        // ── Stored tokens ─────────────────────────────────────────────────
        public string? TwitchAccessToken { get; private set; }
        public string? TwitchRefreshToken { get; private set; }
        public int TwitchExpiresIn { get; private set; }
        public bool StreamerAuthed => !string.IsNullOrEmpty(TwitchAccessToken);

        public string? TwitchBotAccessToken { get; private set; }
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
            TwitchBotExpiresIn = expiresIn;
        }

        public async Task SaveSetupAsync(SetupWizardModel model)
        {
            var webauth = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            var providerString = model.DatabaseProvider switch
            {
                DatabaseProviderOption.MariaDb => "mariadb",
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
                ["twitchBotRefreshToken"] = "",
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
                    ["SqliteConnection"] = model.SqliteFilePath.Trim(),
                    ["MariaDbConnection"] = model.MariaDbConnectionString.Trim(),
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

        private static ulong ParseUlong(string? value) =>
            ulong.TryParse(value?.Trim(), out var result) ? result : 0;
    }
}
