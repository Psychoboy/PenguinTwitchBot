namespace DotNetTwitchBot.Application.Notifications
{
    public interface IRequest<TResponse>
    {
    }

    public interface IRequest : IRequest<Unit>
    {
    }

    public struct Unit
    {
        public static readonly Unit Value = new();
    }
}
