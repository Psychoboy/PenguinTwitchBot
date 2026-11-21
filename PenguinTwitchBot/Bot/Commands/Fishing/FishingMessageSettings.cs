using PenguinTwitchBot.Bot.Commands.Games;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public sealed class FishingMessageSettings
    {
        public const string DefaultFrenzyStart = "🔥 {name} has started for {minutes} min! ({bonus}) Cast your lines now!";
        public const string DefaultFrenzyEnd = "🎣 {name} has ended! Thanks for fishing!";
        public const string DefaultFrenzyStatusActive = "🔥 {name} is ACTIVE ({bonus}) — {remaining} remaining!";
        public const string DefaultFrenzyStatusNone = "🎣 No Fishing Frenzy is active right now.{modHint}";

        public const string DefaultQuickTournamentStart = "🏆 QUICK FISHING TOURNAMENT: {name} is LIVE for {minutes} min! ({category}){frenzyNote}";
        public const string DefaultTournamentFirstLeader = "👑 {leader} takes 1st place in {tournament} with {score}!";
        public const string DefaultTournamentLeadChange = "👑 {leader} just stole 1st place from {previousLeader} in {tournament} ({score})!";
        public const string DefaultTournamentTwoMinuteWarning = "⏳ 2 minutes left in {tournament}!{leaderNote}";
        public const string DefaultTournamentThirtySecondWarning = "⏱️ 30 SECONDS LEFT in {tournament}!{leaderNote} Last casts now!";
        public const string DefaultTournamentEnd = "🏆 {tournament} has ended! {podium}";
        public const string DefaultTournamentEndNoWinners = "🏆 {tournament} has ended with no catches recorded!";
        public const string DefaultTournamentEndTts = "{tournament} has ended! {podium}";
        public const string DefaultTournamentEndTtsNoWinners = "{tournament} has ended with no catches recorded.";

        public const string DefaultStatsResponse = "🎣 {user}: {gold}g | {catches} catches | Dex: {caughtSpecies}/{totalSpecies} ({percent}%){bestPart}";
        public const string DefaultGearResponse = "🎣 {user}'s Gear: {gear}{repairHint}";
        public const string DefaultGearEmptyResponse = "🎣 {user}: No fishing gear equipped! Visit the Fishing Shop to gear up.";
        public const string DefaultRepairSuccessResponse = "🔧 {user}: Repaired all equipped fishing gear for {cost}g! Remaining gold: {gold}g.";
        public const string DefaultRepairNoneNeededResponse = "✅ {user}: All your equipped fishing gear is already at full durability!";
        public const string DefaultDexSummaryResponse = "📖 {user}'s Fish Dex: {caughtSpecies}/{totalSpecies} species ({percent}%). Try !fishdex <fish name> to look up a specific fish!";
        public const string DefaultDexFishCaughtResponse = "📖 {user} — {fish} ({rarity}): Caught {count}x | PB: {stars}★ {weight}kg ({gold}g).";
        public const string DefaultDexFishUncaughtResponse = "📖 {fish} ({rarity}{categories}): Not caught yet! Base: {weight}kg, {gold}g.";

        // Frenzy & Hotspot
        public string FrenzyStart { get; set; } = DefaultFrenzyStart;
        public string FrenzyEnd { get; set; } = DefaultFrenzyEnd;
        public string FrenzyStatusActive { get; set; } = DefaultFrenzyStatusActive;
        public string FrenzyStatusNone { get; set; } = DefaultFrenzyStatusNone;

        // Tournament Hype & Quick Blitz
        public string QuickTournamentStart { get; set; } = DefaultQuickTournamentStart;
        public string TournamentFirstLeader { get; set; } = DefaultTournamentFirstLeader;
        public string TournamentLeadChange { get; set; } = DefaultTournamentLeadChange;
        public string TournamentTwoMinuteWarning { get; set; } = DefaultTournamentTwoMinuteWarning;
        public string TournamentThirtySecondWarning { get; set; } = DefaultTournamentThirtySecondWarning;
        public string TournamentEnd { get; set; } = DefaultTournamentEnd;
        public string TournamentEndNoWinners { get; set; } = DefaultTournamentEndNoWinners;
        public string TournamentEndTts { get; set; } = DefaultTournamentEndTts;
        public string TournamentEndTtsNoWinners { get; set; } = DefaultTournamentEndTtsNoWinners;

        // Chat Command Responses
        public string StatsResponse { get; set; } = DefaultStatsResponse;
        public string GearResponse { get; set; } = DefaultGearResponse;
        public string GearEmptyResponse { get; set; } = DefaultGearEmptyResponse;
        public string RepairSuccessResponse { get; set; } = DefaultRepairSuccessResponse;
        public string RepairNoneNeededResponse { get; set; } = DefaultRepairNoneNeededResponse;
        public string DexSummaryResponse { get; set; } = DefaultDexSummaryResponse;
        public string DexFishCaughtResponse { get; set; } = DefaultDexFishCaughtResponse;
        public string DexFishUncaughtResponse { get; set; } = DefaultDexFishUncaughtResponse;

        /// <summary>
        /// Replaces {token} placeholders in a message template. Returns null if the template is blank (disabled).
        /// </summary>
        public static string? Format(string? template, params (string Key, string Value)[] tokens)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return null;
            }

            var result = template;
            foreach (var (key, value) in tokens)
            {
                result = result.Replace("{" + key + "}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            return result.Trim();
        }

        public FishingMessageSettings Clone() => (FishingMessageSettings)MemberwiseClone();
    }

    public interface IFishingMessageSettingsService
    {
        Task<FishingMessageSettings> GetAsync();
        Task SaveAsync(FishingMessageSettings settings);
    }

    public class FishingMessageSettingsService(IGameSettingsService gameSettingsService) : IFishingMessageSettingsService
    {
        private const string GameName = "Fishing";
        private const string SettingKey = "ChatMessagesJson";
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly object _lock = new();
        private FishingMessageSettings? _cached;
        private long _generation;

        public async Task<FishingMessageSettings> GetAsync()
        {
            long generation;
            lock (_lock)
            {
                if (_cached != null) return _cached.Clone();
                generation = _generation;
            }

            var raw = await gameSettingsService.GetStringSetting(GameName, SettingKey, string.Empty);
            FishingMessageSettings parsed;
            if (string.IsNullOrWhiteSpace(raw))
            {
                parsed = new FishingMessageSettings();
            }
            else
            {
                try
                {
                    parsed = JsonSerializer.Deserialize<FishingMessageSettings>(raw, JsonOptions) ?? new FishingMessageSettings();
                }
                catch
                {
                    parsed = new FishingMessageSettings();
                }
            }

            lock (_lock)
            {
                if (_generation == generation)
                {
                    _cached = parsed;
                }
                return (_cached ?? parsed).Clone();
            }
        }

        public async Task SaveAsync(FishingMessageSettings settings)
        {
            var copy = settings.Clone();
            var json = JsonSerializer.Serialize(copy, JsonOptions);
            await gameSettingsService.SaveSetting(GameName, SettingKey, json);
            lock (_lock)
            {
                _generation++;
                _cached = copy;
            }
        }
    }
}
