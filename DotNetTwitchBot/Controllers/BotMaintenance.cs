using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot;
using DotNetTwitchBot.Bot.Commands.Custom;
using DotNetTwitchBot.Bot.Core.Database;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BotMaintenance : ControllerBase
    {
        private ILogger<BotMaintenance> _logger;
        private TwitchService _twitchService;
        private CustomCommand _customCommand;
        private readonly IServiceScopeFactory _scopeFactory;

        public BotMaintenance(
            ILogger<BotMaintenance> logger,
            TwitchService twitchService,
            IServiceScopeFactory scopeFactory,
            CustomCommand customCommand
            )
        {
            _logger = logger;
            _twitchService = twitchService;
            _scopeFactory = scopeFactory;
            _customCommand = customCommand;
        }

        [HttpGet("/updatefollows")]
        public async Task<ActionResult> UpdateFollows()
        {
            var followers = await _twitchService.GetAllFollows();
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Followers.AddRangeAsync(followers);
                await db.SaveChangesAsync();
            }
            return Ok();
        }

        [IgnoreAntiforgeryToken]
        [HttpPost("/addcommand")]
        public async Task<ActionResult> AddCommand([FromBody] CustomCommands command)
        {
            await _customCommand.AddCommand(command);
            return Ok();
        }
    }
}