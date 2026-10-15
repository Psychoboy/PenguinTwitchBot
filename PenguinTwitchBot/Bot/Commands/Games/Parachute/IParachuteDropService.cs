using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Games.Parachute
{
    public interface IParachuteDropService
    {
        Task<string> TriggerParachuteDropAsync(
            CommandEventArgs eventArgs,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            double? forcedStartX = null,
            double? forcedWindAccelX = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = false);

        Task<List<DropletInstanceDto>> SpawnDropletsAsync(DropletSpawnRequest request);

        Task<DropGameSessionDto> StartSessionAsync(
            int? durationSeconds = null,
            string? themeOverrideId = null,
            string? triggeredBy = null);

        Task<DropGameSessionDto?> StopSessionAsync(string? triggeredBy = null);

        DropGameSessionDto? GetCurrentSession();
        int GetQueuedSessionCount();
        Task ClearSessionQueueAsync();
        Task ProcessCompletionAsync(PendingDropRecord record);
    }
}

