using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Features;

namespace PenguinTwitchBot.Application.DropGameNotifications
{
    public class DropGameCompleteHandler(
        IDropRegistry dropRegistry,
        IParachuteDropService parachuteDropService,
        IPlinkoDropService plinkoDropService,
        IDropGamesSettingsService settingsService,
        IFeatureRuntimeCoordinator featureRuntimeCoordinator)
        : Application.Notifications.INotificationHandler<DropGameCompleteNotification>
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
                if (record.GameType == DropGameType.Parachute)
                {
                    if (notification.LandingX.HasValue &&
                        notification.LandingX.Value >= 0.04 && notification.LandingX.Value <= 0.96)
                    {
                        var landingTolerance = Math.Min(0.85, 0.40 + (record.DropletHitCount * 0.15));
                        if (Math.Abs(notification.LandingX.Value - record.LandingX) <= landingTolerance)
                        {
                            record.LandingX = Math.Round(Math.Clamp(notification.LandingX.Value, 0.05, 0.95), 4);
                            (record.Score, record.IsBullseye, record.IsWin, record.IsHit) =
                                ParachuteSimulationEngine.CalculateLandingScore(record.LandingX, record.TargetCenterX, record.TargetHalfWidth);
                        }
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
                        if (notification.WinningBucketIndex.HasValue)
                        {
                            var idx = notification.WinningBucketIndex.Value;
                            var bucketTolerance = Math.Min(4, 2 + record.DropletHitCount);
                            if (Math.Abs(idx - record.BucketIndex) <= bucketTolerance && idx >= 0 && idx < buckets.Count)
                            {
                                record.BucketIndex = idx;
                            }
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
