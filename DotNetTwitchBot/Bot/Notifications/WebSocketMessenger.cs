using System.Collections.Concurrent;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Notifications
{
    public class WebSocketMessenger : IWebSocketMessenger
    {
        private BlockingCollection<string> _queue = new BlockingCollection<string>();
        public List<SocketConnection> websocketConnections = new List<SocketConnection>();
        ILogger<WebSocketMessenger> _logger;

        public WebSocketMessenger(ILogger<WebSocketMessenger> logger)
        {
            _logger = logger;
            SetupCleanUpTask();
        }

        public void AddToQueue(string message)
        {
            _queue.Add(message);

        }

        public async Task Handle(Guid id, WebSocket webSocket)
        {
            _logger.LogInformation("Adding Websocket: {0}", id.ToString());
            lock (websocketConnections)
            {
                websocketConnections.Add(new SocketConnection
                {
                    Id = id,
                    WebSocket = webSocket
                });
            }

            while (webSocket.State == WebSocketState.Open)
            {
                var result = _queue.Take();
                if (result != null)
                {
                    await SendMessageToSockets(result);
                }
            }
            _logger.LogInformation("Websocket closed: {0}", id.ToString());
        }

        private async Task SendMessageToSockets(string message)
        {
            IEnumerable<SocketConnection> toSentTo;
            lock (websocketConnections)
            {
                toSentTo = websocketConnections.ToList();
            }

            var tasks = toSentTo.Select(async websocketConnection =>
            {
                if (websocketConnection.WebSocket.State == WebSocketState.Open)
                {
                    var bytes = Encoding.Default.GetBytes(message);
                    var arraySegment = new ArraySegment<byte>(bytes);
                    await websocketConnection.WebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            });
            await Task.WhenAll(tasks);
        }

        private void SetupCleanUpTask()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    IEnumerable<SocketConnection> openSockets;
                    IEnumerable<SocketConnection> closedSockets;

                    lock (websocketConnections)
                    {
                        openSockets = websocketConnections.Where(x => x.WebSocket.State == WebSocketState.Open || x.WebSocket.State == WebSocketState.Connecting);
                        closedSockets = websocketConnections.Where(x => x.WebSocket.State != WebSocketState.Open && x.WebSocket.State != WebSocketState.Connecting);

                        websocketConnections = openSockets.ToList();
                    }

                    foreach (var closedWebsocketConnection in closedSockets)
                    {
                        _logger.LogInformation("Closing Socket: {0}", closedWebsocketConnection.Id);
                    }

                    await Task.Delay(5000);
                }

            });
        }

        // public async Task ProcessQueue(WebSocket webSocket)
        // {
        //     while (!webSocket.CloseStatus.HasValue)
        //     {
        //         while (true)
        //         {
        //             var result = _queue.Take();
        //             if (result != null)
        //             {
        //                 await SendMessageToSockets(webSocket, result);
        //             }
        //         }
        //     }
        //     _logger.LogInformation("Websocket closed: {0}", webSocket.CloseStatus.Value.ToString());
        // }

        // private async Task SendMessageToSockets(WebSocket webSocket, string message)
        // {
        //     var bytes = Encoding.Default.GetBytes(message);
        //     var arraySegment = new ArraySegment<byte>(bytes);
        //     await webSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
        // }
    }

    public class SocketConnection
    {
        public Guid Id { get; set; }
        public WebSocket WebSocket { get; set; } = null!;
    }
}