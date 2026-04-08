using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Application.WheelSpinNotifications
{
    public class WheelSpinCompleteNotification(WheelSpinComplete wheelSpinComplete) : INotification
    {
        public WheelSpinComplete WheelSpinComplete { get; private set; } = wheelSpinComplete;
    }
}
