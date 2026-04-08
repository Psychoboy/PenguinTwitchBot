namespace DotNetTwitchBot.Application.Notifications
{
    public interface INotificationHandler<in TNotification> where TNotification : INotification
    {
        Task Handle(TNotification notification, CancellationToken cancellationToken);
    }
}
