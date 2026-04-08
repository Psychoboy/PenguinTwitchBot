namespace DotNetTwitchBot.Application.Notifications
{
    /// <summary>
    /// Handler interface for processing notifications
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to handle</typeparam>
    public interface INotificationHandler<in TNotification> where TNotification : INotification
    {
        Task Handle(TNotification notification, CancellationToken cancellationToken);
    }
}
