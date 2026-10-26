using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Games.Racing
{
    public interface IRacingGameService
    {
        Task<DropGameSessionDto> StartRaceLobbyAsync(
            int? lobbyDurationSeconds = null,
            string? themeOverrideId = null,
            string? triggeredBy = null,
            RacingGameMode? gameModeOverride = null,
            RacingTrackType? trackTypeOverride = null,
            int? lapsOverride = null);

        Task<DropGameSessionDto?> StopSessionAsync(string? triggeredBy = null);

        Task<string> TriggerJoinRaceAsync(
            CommandEventArgs eventArgs,
            string? emoteOrVehicle = null,
            string? themeOverrideId = null,
            bool allowDirectUrlOrPath = true);

        Task<string> TriggerSoloTimeTrialAsync(
            CommandEventArgs eventArgs,
            string? emoteOrVehicle = null,
            string? themeOverrideId = null,
            bool allowDirectUrlOrPath = true);

        DropGameSessionDto? GetCurrentSession();
        int GetQueuedSessionCount();
        Task ClearSessionQueueAsync();
        Task ProcessCompletionAsync(PendingDropRecord record);
        Task<int> AddTestRacersAsync(
            int count,
            string? emoteOrVehicle = null,
            bool autoLaunch = false,
            RacingGameMode? gameModeOverride = null,
            RacingTrackType? trackTypeOverride = null);
        Task LaunchRaceNowAsync(string? triggeredBy = null);
    }
}

