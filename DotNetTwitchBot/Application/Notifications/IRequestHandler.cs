namespace DotNetTwitchBot.Application.Notifications
{
    /// <summary>
    /// Handler interface for processing requests without return value
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle</typeparam>
    public interface IRequestHandler<in TRequest> where TRequest : IRequest
    {
        Task Handle(TRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Handler interface for processing requests with return value
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle</typeparam>
    /// <typeparam name="TResponse">The type of response to return</typeparam>
    public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
    }
}
