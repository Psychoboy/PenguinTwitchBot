using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [Route("/ws")]
    public class WebSocketController : ControllerBase
    {
        private IWebSocketMessenger WebSocketMessenger { get; }

        public WebSocketController(IWebSocketMessenger webSocketMessenger)
        {
            WebSocketMessenger = webSocketMessenger;
        }
        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                // await Echo(webSocket);
                await WebSocketMessenger.Handle(Guid.NewGuid(), webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}