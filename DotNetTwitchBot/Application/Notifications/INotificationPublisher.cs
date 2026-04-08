namespace DotNetTwitchBot.Application.Notifications
{
    /// <summary>
    /// Publisher interface for sending notifications to handlers
    /// </summary>
    public interface INotificationPublisher
    {
        Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
    }
}
