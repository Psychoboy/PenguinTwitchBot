using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Features;

namespace PenguinTwitchBot.Application.DropGameNotifications
{
    public class DropGameHeartbeatHandler(
        IDropRegistry dropRegistry,
        IDropGamesSettingsService settingsService,
        IFeatureRuntimeCoordinator featureRuntimeCoordinator,
        ILogger<DropGameHeartbeatHandler> logger)
        : Notifications.INotificationHandler<DropGameHeartbeatNotification>
    {
        public const int MaxMissedHeartbeats = 3;

        public async Task Handle(DropGameHeartbeatNotification notification, CancellationToken cancellationToken)
        {
            if (!featureRuntimeCoordinator.IsEnabled(FeatureKeys.DropGames))
            {
                return;
            }

            var settings = await settingsService.GetSettingsAsync();
            var authorizedSecretKey = settings.OverlaySecretKey;

            // Only heartbeats from authorized overlays with the correct secret key are trusted
            if (string.IsNullOrWhiteSpace(authorizedSecretKey) ||
                !string.Equals(notification.OverlayKey, authorizedSecretKey, StringComparison.Ordinal))
            {
                return;
            }

            DropGameType? gameType = notification.Game?.ToLowerInvariant() switch
            {
                "parachute" => DropGameType.Parachute,
                "plinko" => DropGameType.Plinko,
                _ => null
            };

            if (!gameType.HasValue)
            {
                return;
            }

            var activeDrops = dropRegistry.GetActiveDrops(gameType.Value);
            if (activeDrops.Count == 0)
            {
                return;
            }

            var reportedActive = new HashSet<string>(notification.ActiveTokens ?? [], StringComparer.Ordinal);
            var reportedCompleting = new HashSet<string>(notification.CompletingTokens ?? [], StringComparer.Ordinal);
            var now = DateTime.UtcNow;

            foreach (var drop in activeDrops)
            {
                if (reportedActive.Contains(drop.DropToken) || reportedCompleting.Contains(drop.DropToken))
                {
                    drop.LastHeartbeatUtc = now;
                    drop.MissedHeartbeatCount = 0;

                    // Ensure active prolonged flight/bouncing drops do not expire while visibly rendering
                    if (drop.ExpiresAtUtc < now.AddSeconds(15))
                    {
                        drop.ExpiresAtUtc = now.AddSeconds(15);
                    }
                }
                else
                {
                    // Only count missed heartbeats after the drop had sufficient time to start rendering on the client
                    if (now > drop.EarliestCompletionUtc)
                    {
                        drop.MissedHeartbeatCount++;
                        if (drop.MissedHeartbeatCount >= MaxMissedHeartbeats)
                        {
                            logger.LogWarning(
                                "Drop {DropToken} ({GameType}) for user {User} missing from overlay heartbeat for {Count} cycles after expected duration. Cleaning up dropped token.",
                                drop.DropToken,
                                drop.GameType,
                                drop.Username,
                                drop.MissedHeartbeatCount);

                            dropRegistry.AcknowledgeCompletion(drop.DropToken);
                        }
                    }
                }
            }
        }
    }
}

