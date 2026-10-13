using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Games
{
    public interface IDropGamesService
    {
        Task<DropGamesSettings> GetSettingsAsync();
        Task SaveSettingsAsync(DropGamesSettings settings);

        Task<string> TriggerParachuteDropAsync(
            CommandEventArgs eventArgs,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            double? forcedStartX = null,
            double? forcedWindAccelX = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = false);

        Task<List<DropletInstanceDto>> SpawnDropletsAsync(DropletSpawnRequest request);

        Task<string> TriggerPlinkoDropAsync(
            CommandEventArgs eventArgs,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            int? forcedBucketIndex = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = false);

        Task<bool> CompleteDropAsync(string dropToken);

        PendingDropRecord? GetPendingDrop(string dropToken);

        Task<DropGameSessionDto> StartSessionAsync(
            DropGameType gameType,
            int? durationSeconds = null,
            string? themeOverrideId = null,
            string? triggeredBy = null);

        Task<DropGameSessionDto?> StopSessionAsync(
            DropGameType gameType,
            string? triggeredBy = null);

        DropGameSessionDto? GetCurrentSession(DropGameType gameType);
    }
}

