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
        private FollowData _followData;

        public BotMaintenance(
            ILogger<BotMaintenance> logger, 
            TwitchService twitchService, 
            FollowData followData
            )
        {
            _logger = logger;
            _twitchService = twitchService;
            _followData = followData;
        }

        [HttpGet("/updatefollows")]
        public async Task<ActionResult> UpdateFollows()
        {
            var followers = await _twitchService.GetAllFollows();
            await _followData.InsertAll(followers);
            return Ok();
        }
    }
}