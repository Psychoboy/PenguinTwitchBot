using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Features;

namespace PenguinTwitchBot.Application.DropGameNotifications
{
    public class DropGameCompleteHandler(
        IDropGamesService dropGamesService,
        IFeatureRuntimeCoordinator featureRuntimeCoordinator)
        : Application.Notifications.INotificationHandler<DropGameCompleteNotification>
    {
        public async Task Handle(DropGameCompleteNotification notification, CancellationToken cancellationToken)
        {
            if (!featureRuntimeCoordinator.IsEnabled(FeatureKeys.DropGames))
            {
                return;
            }

            await dropGamesService.CompleteDropAsync(notification.DropToken);
        }
    }
}

