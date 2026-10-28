using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using Microsoft.AspNetCore.Mvc;

namespace PenguinTwitchBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BotCommandsController(
        ILogger<BotCommandsController> logger,
        IConfiguration configuration,
        IServiceBackbone serviceBackbone,
        IActionManagementService actionManagementService,
        IAction actionExecutor) : ControllerBase
    {
        [HttpPut("/commands")]
        public async Task<ActionResult> RunCommand([FromHeader] string webauth, [FromHeader] string user, [FromHeader] string message)
        {
            logger.LogInformation("{ipAddress} accessed /commands.", HttpContext.Connection?.RemoteIpAddress);
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
                    TargetUser = "",
                    SkipLock = true
                };
                await serviceBackbone.RunCommand(args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Controller");
            }
            return new OkResult();
        }

        /// <summary>
        /// Queues an action by name or id. Query string values are passed to the action as variables,
        /// e.g. POST /actions/Shoutout?target=someuser
        /// </summary>
        [HttpPost("/actions/{idOrName}")]
        [HttpPut("/actions/{idOrName}")]
        public async Task<ActionResult> RunAction([FromHeader] string webauth, string idOrName, [FromHeader] string? user = null)
        {
            logger.LogInformation("{ipAddress} accessed /actions.", HttpContext.Connection?.RemoteIpAddress);
            var expectedAuth = configuration["webauth"];
            if (string.IsNullOrEmpty(expectedAuth) || !webauth.Equals(expectedAuth))
            {
                return new StatusCodeResult(403);
            }

            var actionType = Guid.TryParse(idOrName, out var actionId)
                ? await actionManagementService.GetActionByIdAsync(actionId)
                : (await actionManagementService.GetAllActionsAsync())
                    .FirstOrDefault(a => a.Name.Equals(idOrName, StringComparison.OrdinalIgnoreCase));

            if (actionType == null)
            {
                return NotFound($"Action '{idOrName}' not found.");
            }
            if (!actionType.Enabled)
            {
                return Conflict($"Action '{actionType.Name}' is disabled.");
            }

            var name = string.IsNullOrWhiteSpace(user) ? serviceBackbone.BroadcasterName : user;
            var variables = CommandEventArgsConverter.ToDictionary(new CommandEventArgs
            {
                Name = name.ToLower(),
                DisplayName = name,
                UserId = "0",
                IsSub = true,
                IsVip = true,
                IsMod = true,
                IsBroadcaster = true,
                SkipLock = true
            });
            variables["triggertype"] = "Api";
            variables["source"] = "ApiActions";
            foreach (var (key, value) in Request.Query)
            {
                variables[key] = value.ToString();
            }

            await actionExecutor.EnqueueAction(variables, actionType);
            return Accepted(new { actionType.Id, actionType.Name, actionType.QueueName });
        }
    }
}
