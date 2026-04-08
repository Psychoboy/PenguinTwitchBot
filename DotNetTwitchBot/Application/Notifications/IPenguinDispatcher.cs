namespace DotNetTwitchBot.Application.Notifications
{
    public interface IPenguinDispatcher
    {
        Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
        Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    }
}
