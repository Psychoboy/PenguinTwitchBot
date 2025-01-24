using DotNetTwitchBot.Bot.Commands.WheelSpin;
using MediatR;

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
