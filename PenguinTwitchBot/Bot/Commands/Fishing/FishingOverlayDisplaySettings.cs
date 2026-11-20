using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Notifications;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public sealed class FishingOverlayDisplaySettings
    {
        public const string ModeAuto = "Auto";
        public const string ModeAlways = "Always";
        public const string ModeOnCast = "OnCast";
        public const string ModePeriodic = "Periodic";
        public const string ModeHidden = "Hidden";

        /// <summary>
        /// Controls when the Frenzy banner on /fishing.html is displayed:
        /// Auto (Always for short events &lt;= threshold, OnCast + Periodic for longer),
        /// OnCast (only while casting + start + final 2m),
        /// Periodic (pops in every N minutes + start + final 2m),
        /// Always, or Hidden (still shows FRENZY badge on catch cards).
        /// </summary>
        public string FrenzyBannerMode { get; set; } = ModeAuto;
        public int FrenzyReminderIntervalMinutes { get; set; } = 5;
        public int FrenzyReminderDurationSeconds { get; set; } = 12;

        /// <summary>
        /// Controls when /fishing-tournaments.html is visible during active tournaments:
        /// Always, Auto (Always for short events &lt;= threshold, OnCast + Periodic for longer),
        /// OnCast (pops in on catches/lead changes + final 2m + podium), or
        /// Periodic (pops in every N minutes + lead changes + final 2m + podium).
        /// </summary>
        public string TournamentOverlayMode { get; set; } = ModeAuto;
        public int TournamentReminderIntervalMinutes { get; set; } = 5;
        public int TournamentReminderDurationSeconds { get; set; } = 15;

        /// <summary>
        /// In Auto mode, events whose total duration is at or below this threshold (in minutes)
        /// stay Always Visible; longer events automatically switch to non-intrusive reminders.
        /// </summary>
        public int AutoAlwaysThresholdMinutes { get; set; } = 15;

        public void Normalize()
        {
            FrenzyBannerMode = FrenzyBannerMode is ModeAuto or ModeAlways or ModeOnCast or ModePeriodic or ModeHidden
                ? FrenzyBannerMode
                : ModeAuto;
            TournamentOverlayMode = TournamentOverlayMode is ModeAuto or ModeAlways or ModeOnCast or ModePeriodic
                ? TournamentOverlayMode
                : ModeAuto;
            FrenzyReminderIntervalMinutes = Math.Clamp(FrenzyReminderIntervalMinutes, 1, 60);
            FrenzyReminderDurationSeconds = Math.Clamp(FrenzyReminderDurationSeconds, 5, 60);
            TournamentReminderIntervalMinutes = Math.Clamp(TournamentReminderIntervalMinutes, 1, 60);
            TournamentReminderDurationSeconds = Math.Clamp(TournamentReminderDurationSeconds, 5, 60);
            AutoAlwaysThresholdMinutes = Math.Clamp(AutoAlwaysThresholdMinutes, 1, 120);
        }
    }

    public interface IFishingOverlayDisplaySettingsService
    {
        Task<FishingOverlayDisplaySettings> GetAsync();
        Task SaveAsync(FishingOverlayDisplaySettings settings);
    }

    public class FishingOverlayDisplaySettingsService(
        IGameSettingsService gameSettingsService,
        IWebSocketMessenger? webSocketMessenger = null) : IFishingOverlayDisplaySettingsService
    {
        private const string GameName = "Fishing";
        private const string SettingKey = "OverlayDisplayJson";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly object _lock = new();
        private FishingOverlayDisplaySettings? _cached;

        public async Task<FishingOverlayDisplaySettings> GetAsync()
        {
            lock (_lock)
            {
                if (_cached != null) return _cached;
            }

            var raw = await gameSettingsService.GetStringSetting(GameName, SettingKey, string.Empty);
            FishingOverlayDisplaySettings parsed;
            if (string.IsNullOrWhiteSpace(raw))
            {
                parsed = new FishingOverlayDisplaySettings();
            }
            else
            {
                try
                {
                    parsed = JsonSerializer.Deserialize<FishingOverlayDisplaySettings>(raw, JsonOptions)
                        ?? new FishingOverlayDisplaySettings();
                }
                catch
                {
                    parsed = new FishingOverlayDisplaySettings();
                }
            }

            parsed.Normalize();
            lock (_lock)
            {
                _cached = parsed;
            }
            return parsed;
        }

        public async Task SaveAsync(FishingOverlayDisplaySettings settings)
        {
            settings.Normalize();
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await gameSettingsService.SaveSetting(GameName, SettingKey, json);
            lock (_lock)
            {
                _cached = settings;
            }

            if (webSocketMessenger != null)
            {
                var payload = JsonSerializer.Serialize(new { fishingOverlayConfig = settings }, JsonOptions);
                await webSocketMessenger.AddToQueue(WsTopics.Fishing, payload);
            }
        }
    }
}
