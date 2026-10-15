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
                    if (notification.LandingX.HasValue && notification.LandingX.Value >= 0.04 && notification.LandingX.Value <= 0.96)
                    {
                        record.LandingX = Math.Round(Math.Clamp(notification.LandingX.Value, 0.05, 0.95), 4);
                        var distFromCenter = Math.Round(Math.Abs(record.LandingX - record.TargetCenterX), 4);
                        record.IsHit = distFromCenter <= record.TargetHalfWidth;
                        if (record.IsHit)
                        {
                            var ratio = Math.Clamp(distFromCenter / Math.Max(0.0001, record.TargetHalfWidth), 0.0, 1.0);
                            record.Score = Math.Clamp((int)Math.Round(100.0 - (ratio * 99.0), MidpointRounding.AwayFromZero), 1, 100);
                            record.IsBullseye = record.Score == 100;
                            record.IsWin = true;
                        }
                        else
                        {
                            record.Score = 0;
                            record.IsBullseye = false;
                            record.IsWin = false;
                        }
                    }

                    await parachuteDropService.ProcessCompletionAsync(record);
                }
                else if (record.GameType == DropGameType.Plinko)
                {
                    if (notification.WinningBucketIndex.HasValue)
                    {
                        var settings = await settingsService.GetSettingsAsync();
                        var buckets = settings.Plinko.Buckets;
                        var idx = notification.WinningBucketIndex.Value;
                        if (idx >= 0 && idx < buckets.Count)
                        {
                            var b = buckets[idx];
                            record.BucketIndex = idx;
                            record.BucketLabel = b.Label;
                            record.Score = b.Score;
                            record.IsWin = b.IsWin;
                        }
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
