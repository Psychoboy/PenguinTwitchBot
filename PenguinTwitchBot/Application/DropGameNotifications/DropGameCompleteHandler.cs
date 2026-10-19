using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Bot.Overlay;

namespace PenguinTwitchBot.Application.DropGameNotifications
{
    public class DropGameCompleteHandler(
        IDropRegistry dropRegistry,
        IParachuteDropService parachuteDropService,
        IPlinkoDropService plinkoDropService,
        IDropGamesSettingsService settingsService,
        IOverlaySecurityService overlaySecurityService,
        IFeatureRuntimeCoordinator featureRuntimeCoordinator)
        : Notifications.INotificationHandler<DropGameCompleteNotification>
    {
        public async Task Handle(DropGameCompleteNotification notification, CancellationToken cancellationToken)
        {
            if (!featureRuntimeCoordinator.IsEnabled(FeatureKeys.DropGames))
            {
                return;
            }

            if (!dropRegistry.TryBeginCompletion(notification.DropToken, out var record, out _) || record == null)
            {
                return;
            }

            try
            {
                var isAuthorized = await overlaySecurityService.ValidateKeyAsync(notification.OverlayKey);

                if (!isAuthorized)
                {
                    // Unauthorized client completion: discard client-reported outcome and fall back to authoritative server simulation
                    notification.LandingX = null;
                    notification.WinningBucketIndex = null;
                }

                if (record.GameType == DropGameType.Parachute)
                {
                    if (notification.LandingX.HasValue)
                    {
                        // Authorized client with secret key: trust the client's rendered landing position directly
                        record.LandingX = Math.Round(Math.Clamp(notification.LandingX.Value, 0.05, 0.95), 4);
                        if (notification.ParachuteDestroyed.HasValue)
                        {
                            record.ParachuteDestroyed = notification.ParachuteDestroyed.Value;
                        }

                        (record.Score, record.IsBullseye, record.IsWin, record.IsHit) =
                            ParachuteSimulationEngine.CalculateLandingScore(
                                record.LandingX,
                                record.TargetCenterX,
                                record.TargetHalfWidth,
                                record.ParachuteDestroyed,
                                record.CrashLandingScoreMode);
                    }

                    await parachuteDropService.ProcessCompletionAsync(record);
                }
                else if (record.GameType == DropGameType.Plinko)
                {
                    var buckets = record.Buckets;
                    if (buckets == null || buckets.Count == 0)
                    {
                        var settings = await settingsService.GetSettingsAsync();
                        buckets = settings.Plinko.Buckets;
                    }

                    if (buckets != null && buckets.Count > 0)
                    {
                        if (!record.ForcedOutcome && notification.WinningBucketIndex.HasValue)
                        {
                            // Authorized client with secret key: trust the client's final bucket landing directly
                            var idx = notification.WinningBucketIndex.Value;
                            if (idx >= 0 && idx < buckets.Count)
                            {
                                record.BucketIndex = idx;
                            }
                        }

                        if (record.BucketIndex < 0)
                        {
                            dropRegistry.AcknowledgeCompletion(notification.DropToken);
                            return;
                        }

                        var selectedIdx = Math.Clamp(record.BucketIndex, 0, buckets.Count - 1);
                        var b = buckets[selectedIdx];
                        record.BucketIndex = selectedIdx;
                        record.BucketLabel = b.Label;
                        record.Score = b.IsWin ? b.Score : 0;
                        record.IsWin = b.IsWin;
                        record.IsHit = b.IsWin;
                        var maxWinScore = buckets.Where(x => x.IsWin).Select(x => x.Score).DefaultIfEmpty(0).Max();
                        record.IsBullseye = b.IsWin && b.Score >= maxWinScore;
                    }
                    else if (record.BucketIndex < 0)
                    {
                        dropRegistry.AcknowledgeCompletion(notification.DropToken);
                        return;
                    }

                    await plinkoDropService.ProcessCompletionAsync(record);
                }

                dropRegistry.AcknowledgeCompletion(notification.DropToken);
            }
            finally
            {
                lock (record)
                {
                    if (!record.IsCompleted)
                    {
                        record.IsProcessing = false;
                    }
                }
            }
        }
    }
}
