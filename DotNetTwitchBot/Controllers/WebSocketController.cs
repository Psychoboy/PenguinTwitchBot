using DotNetTwitchBot.Bot.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [Route("/ws")]
    public class WebSocketController(IWebSocketMessenger webSocketMessenger) : ControllerBase
    {
        private IWebSocketMessenger WebSocketMessenger { get; } = webSocketMessenger;

        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await WebSocketMessenger.Handle(Guid.NewGuid(), webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}