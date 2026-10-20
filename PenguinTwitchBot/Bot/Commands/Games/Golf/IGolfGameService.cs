using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Games.Golf
{
    public interface IGolfGameService
    {
        Task<string> TriggerGolfShotAsync(
            CommandEventArgs eventArgs,
            double? power = null,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = true,
            GolfGreenCondition? conditionOverride = null,
            double? targetDistanceOverride = null);

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

