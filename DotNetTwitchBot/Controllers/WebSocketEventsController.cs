using DotNetTwitchBot.Bot.WebSocketEvents;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [Route("/ws/events")]
    public class WebSocketEventsController(ILogger<WebSocketEventsController> logger, IWsEventHandler wsEventHandler) : ControllerBase
    {
        public async Task GetEvents()
        {
            logger.LogInformation("{ipAddress} accessed /ws/events.", HttpContext.Connection?.RemoteIpAddress);
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await wsEventHandler.Handle(Guid.NewGuid(), webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}
