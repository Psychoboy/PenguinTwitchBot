using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Games.Balloon
{
    public class BalloonHazardSpawnRequest
    {
        public string Pattern { get; set; } = "TopRain";
        public string HazardType { get; set; } = "Nudge"; // Nudge, HeavyPush, WindGust, DartPop, Random
        public int Count { get; set; } = 8;
        public string? EmoteOrImage { get; set; }
        public string SpeedPreset { get; set; } = "Normal";
        public string SizePreset { get; set; } = "Medium";
        public string? TargetDropToken { get; set; }
        public string? TargetUser { get; set; }
        public string? TriggeredBy { get; set; }
    }

    public interface IBalloonAscentService
    {
        Task<string> TriggerBalloonAscentAsync(
            CommandEventArgs eventArgs,
            string? emoteOrImage = null,
            string? themeOverrideId = null,
            double? forcedStartX = null,
            bool skipMinimumDurationCheck = false,
            bool allowDirectUrlOrPath = true,
            double? forcedInitialVx = null,
            double? forcedTargetCenterX = null);

        Task<List<DropletInstanceDto>> SpawnHazardsAsync(BalloonHazardSpawnRequest request);

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

