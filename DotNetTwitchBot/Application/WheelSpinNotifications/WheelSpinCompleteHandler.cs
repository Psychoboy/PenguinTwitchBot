using DotNetTwitchBot.Bot.Commands.WheelSpin;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Application.WheelSpinNotifications
{
    public class WheelSpinCompleteHandler(IWheelService wheelService) : INotificationHandler<WheelSpinCompleteNotification>
    {
        public async Task Handle(WheelSpinCompleteNotification notification, CancellationToken cancellationToken)
        {
            await wheelService.ValidateAndProcessWinner(notification.WheelSpinComplete.Index);
        }
    }
}
