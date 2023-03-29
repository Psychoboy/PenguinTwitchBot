using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Music;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MusicController : ControllerBase
    {
        private YtPlayer _ytPlayer;

        public MusicController(YtPlayer ytPlayer)
        {
            _ytPlayer = ytPlayer;
        }

        [HttpGet("/music/pause")]
        public async Task<ActionResult> Pause()
        {
            await _ytPlayer.Pause();
            return Ok();
        }

        [HttpGet("/music/nextsong")]
        public async Task<ActionResult> NextSong()
        {
            await _ytPlayer.PlayNextSong();
            return Ok();
        }
    }
}