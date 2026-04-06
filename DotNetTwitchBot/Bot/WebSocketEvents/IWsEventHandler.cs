using System.Net.WebSockets;

namespace DotNetTwitchBot.Bot.WebSocketEvents
{
    public interface IWsEventHandler
    {
        Task AddToQueue(WsEvent evt);
        Task CloseAllSockets();
        Task Handle(Guid id, WebSocket webSocket);
        void Pause();
        void Resume();
    }
}