namespace DotNetTwitchBot.Application.Notifications
{
    /// <summary>
    /// Penguin dispatcher interface for sending requests and publishing notifications
    /// </summary>
    public interface IPenguinDispatcher
    {
        Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
        Task Send(IRequest request, CancellationToken cancellationToken = default);
        Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
    }
}
