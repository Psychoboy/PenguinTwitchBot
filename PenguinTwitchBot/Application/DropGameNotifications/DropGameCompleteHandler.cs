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
        IFeatureRuntimeCoordinator featureRuntimeCoordinator)
        : Application.Notifications.INotificationHandler<DropGameCompleteNotification>
    {
        public async Task Handle(DropGameCompleteNotification notification, CancellationToken cancellationToken)
        {
            if (!featureRuntimeCoordinator.IsEnabled(FeatureKeys.DropGames))
            {
                return;
            }

            if (!dropRegistry.TryConsumeDrop(notification.DropToken, out var record, out _) || record == null)
            {
                return;
            }

            if (record.GameType == DropGameType.Parachute)
            {
                await parachuteDropService.ProcessCompletionAsync(record);
            }
            else if (record.GameType == DropGameType.Plinko)
            {
                await plinkoDropService.ProcessCompletionAsync(record);
            }
        }
    }
}
