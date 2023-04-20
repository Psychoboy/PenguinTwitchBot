using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

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