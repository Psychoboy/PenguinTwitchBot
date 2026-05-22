using PenguinTwitchBot.Bot.Commands.WheelSpin;

namespace PenguinTwitchBot.Application.WheelSpinNotifications
{
    public class WheelSpinCompleteHandler(IWheelService wheelService) : Application.Notifications.INotificationHandler<WheelSpinCompleteNotification>
    {
        public async Task Handle(WheelSpinCompleteNotification notification, CancellationToken cancellationToken)
        {
            await wheelService.ValidateAndProcessWinner(notification.WheelSpinComplete.Index);
        }
    }
}
