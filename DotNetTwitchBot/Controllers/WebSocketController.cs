using DotNetTwitchBot.Bot.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [Route("/ws")]
    public class WebSocketController(IWebSocketMessenger webSocketMessenger, ILogger<WebSocketController> logger) : ControllerBase
    {
        private IWebSocketMessenger WebSocketMessenger { get; } = webSocketMessenger;
        private ILogger<WebSocketController> logger { get; } = logger;

        public async Task Get()
        {
            logger.LogInformation("{ipAddress} accessed /ws.", HttpContext.Connection?.RemoteIpAddress);
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