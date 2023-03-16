using System.Collections.Concurrent;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Notifications
{
    public class WebSocketMessenger
    {
        private BlockingCollection<string> _queue = new BlockingCollection<string>();
        ILogger<WebSocketMessenger> _logger;

        public WebSocketMessenger(ILogger<WebSocketMessenger> logger)
        {
            _logger = logger;
        }

        public void AddToQueue(string message)
        {
            _queue.Add(message);

        }

        public async Task ProcessQueue(WebSocket webSocket)
        {
            while (!webSocket.CloseStatus.HasValue)
            {
                while (true)
                {
                    var result = _queue.Take();
                    if (result != null)
                    {
                        await SendMessageToSockets(webSocket, result);
                    }
                }
            }
            _logger.LogInformation("Websocket closed: {0}", webSocket.CloseStatus.Value.ToString());
        }

        private async Task SendMessageToSockets(WebSocket webSocket, string message)
        {
            var bytes = Encoding.Default.GetBytes(message);
            var arraySegment = new ArraySegment<byte>(bytes);
            await webSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}