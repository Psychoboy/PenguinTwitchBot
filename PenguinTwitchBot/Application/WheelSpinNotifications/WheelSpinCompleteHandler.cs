using PenguinTwitchBot.Bot.Commands.WheelSpin;
using PenguinTwitchBot.Bot.Features;

namespace PenguinTwitchBot.Application.WheelSpinNotifications
{
    public class WheelSpinCompleteHandler(
        IWheelService wheelService,
        IFeatureRuntimeCoordinator featureRuntimeCoordinator) : Application.Notifications.INotificationHandler<WheelSpinCompleteNotification>
    {
        public async Task Handle(WheelSpinCompleteNotification notification, CancellationToken cancellationToken)
        {
            if (!featureRuntimeCoordinator.IsEnabled(FeatureKeys.WheeledGame))
            {
                return;
            }

            await wheelService.ValidateAndProcessWinner(notification.WheelSpinComplete.Index);
        }
    }
}
