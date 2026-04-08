namespace DotNetTwitchBot.Application.WheelSpinNotifications
{
    public class WheelSpinCompleteNotification(WheelSpinComplete wheelSpinComplete) : Application.Notifications.INotification
    {
        public WheelSpinComplete WheelSpinComplete { get; private set; } = wheelSpinComplete;
    }
}
