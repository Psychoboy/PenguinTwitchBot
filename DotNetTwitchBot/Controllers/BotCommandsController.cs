using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BotCommandsController(
        ILogger<BotCommandsController> logger,
        IConfiguration configuration,
        IServiceBackbone serviceBackbone
            )
    {
        [HttpPut("/commands")]
        public async Task<ActionResult> RunCommand([FromHeader] string webauth, [FromHeader] string user, [FromHeader] string message)
        {
            if (!webauth.Equals(configuration["webauth"]))
            {
                return new StatusCodeResult(403);
            }
            try
            {
                var messageArg = "";
                var command = message;
                var indexOfSpace = message.IndexOf(' ');
                if (indexOfSpace >= 0)
                {
                    messageArg = message[indexOfSpace..].Trim();
                    command = message[..indexOfSpace].Trim();
                }
                var args = new CommandEventArgs
                {
                    Arg = messageArg,
                    Args = [.. messageArg.Split(" ")],
                    Command = command[1..],
                    IsWhisper = true,
                    Name = user.ToLower(),
                    DisplayName = user,
                    IsSub = true,
                    IsMod = true,
                    IsBroadcaster = true,
                    TargetUser = ""
                };
                await serviceBackbone.RunCommand(args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Controller");
            }
            return new OkResult();
        }
    }
}