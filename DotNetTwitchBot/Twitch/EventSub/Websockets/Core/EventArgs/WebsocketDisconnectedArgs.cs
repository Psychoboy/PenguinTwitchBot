using System.Net.WebSockets;

namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs
{
    public class WebsocketDisconnectedArgs : System.EventArgs
    {
        public WebSocketCloseStatus CloseStatus { get; set; }
        public string CloseStatusDescription { get; set; } = default!;
    }
}