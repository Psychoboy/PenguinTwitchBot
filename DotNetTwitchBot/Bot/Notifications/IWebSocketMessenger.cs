using System.Net.WebSockets;

namespace DotNetTwitchBot.Bot.Notifications
{
    public interface IWebSocketMessenger
    {
        void AddToQueue(string message);
        Task Handle(Guid id, WebSocket webSocket);

        Task CloseAllSockets();
        void Pause();
        void Resume();
    }
}