using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Games.Plinko
{
    public interface IPlinkoDropService
    {
        Task<string> TriggerPlinkoDropAsync(
            CommandEventArgs eventArgs,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            int? forcedBucketIndex = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = false);

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

