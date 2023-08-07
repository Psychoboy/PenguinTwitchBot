using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BotCommandsController
    {
        readonly ServiceBackbone _serviceBackbone;
        private readonly ILogger<BotCommandsController> _logger;
        private readonly IConfiguration _configuration;

        public BotCommandsController(
            ILogger<BotCommandsController> logger,
            IConfiguration configuration,
            ServiceBackbone serviceBackbone
            )
        {
            _serviceBackbone = serviceBackbone;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPut("/commands")]
        public async Task<ActionResult> RunCommand([FromHeader] string webauth, [FromHeader] string user, [FromHeader] string message)
        {
            if (!webauth.Equals(_configuration["webauth"]))
            {
                return new StatusCodeResult(403);
            }
            try
            {
                var messageArg = "";
                var command = message;
                var indexOfSpace = message.IndexOf(" ");
                if (indexOfSpace >= 0)
                {
                    messageArg = message[indexOfSpace..].Trim();
                    command = message[..indexOfSpace].Trim();
                }
                var args = new CommandEventArgs
                {
                    Arg = messageArg,
                    Args = messageArg.Split(" ").ToList(),
                    Command = command[1..],
                    IsWhisper = true,
                    Name = user.ToLower(),
                    DisplayName = user,
                    IsSub = true,
                    IsMod = true,
                    IsBroadcaster = true,
                    TargetUser = ""
                };
                await _serviceBackbone.RunCommand(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Controller");
            }
            return new OkResult();
        }
    }
}