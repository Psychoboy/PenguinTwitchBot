namespace DotNetTwitchBot.Application.Notifications
{
    /// <summary>
    /// Marker interface for request objects (CQRS pattern)
    /// </summary>
    public interface IRequest
    {
    }

    /// <summary>
    /// Request interface with a return value
    /// </summary>
    /// <typeparam name="TResponse">The type of response returned</typeparam>
    public interface IRequest<out TResponse>
    {
    }
}
