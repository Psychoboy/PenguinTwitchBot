using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Notifications
{
    public class WebsocketHandler
    {
        public List<SocketConnection> websocketConnections = new List<SocketConnection>();

        public async Task SendMessageToSockets(string message)
        {
            IEnumerable<SocketConnection> toSentTo;

            lock (websocketConnections)
            {
                toSentTo = websocketConnections.ToList();
            }

            var tasks = toSentTo.Select(async websocketConnection =>
            {
                var bytes = Encoding.Default.GetBytes(message);
                var arraySegment = new ArraySegment<byte>(bytes);
                if(websocketConnection.WebSocket != null && websocketConnection.WebSocket.State == WebSocketState.Open) {
                    try{
                        await websocketConnection.WebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }catch (Exception e) {
                        
                    }
                }
            });
            await Task.WhenAll(tasks);
        }
    }
    public class SocketConnection
    {
        public Guid Id { get; set; }
        public WebSocket? WebSocket { get; set; }
    }
}