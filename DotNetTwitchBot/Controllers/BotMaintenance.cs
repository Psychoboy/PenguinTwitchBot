using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot;
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
        private readonly IServiceScopeFactory _scopeFactory;

        public BotMaintenance(
            ILogger<BotMaintenance> logger,
            TwitchService twitchService,
            IServiceScopeFactory scopeFactory
            )
        {
            _logger = logger;
            _twitchService = twitchService;
            _scopeFactory = scopeFactory;
        }

        [HttpGet("/updatefollows")]
        public async Task<ActionResult> UpdateFollows()
        {
            var followers = await _twitchService.GetAllFollows();
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Followers.AddRangeAsync(followers);
            }
            return Ok();
        }
    }
}