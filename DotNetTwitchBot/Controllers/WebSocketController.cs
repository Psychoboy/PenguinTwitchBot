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
        private WebSocketMessenger _webSocketMessenger;

        public WebSocketController(WebSocketMessenger webSocketMessenger) {
            _webSocketMessenger = webSocketMessenger;
        }
        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest) 
            {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                // await Echo(webSocket);
                await _webSocketMessenger.ProcessQueue(webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}