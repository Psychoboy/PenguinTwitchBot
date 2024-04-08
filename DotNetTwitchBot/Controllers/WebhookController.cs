using DotNetTwitchBot.Bot.Commands.PastyGames;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [AllowAnonymous]
    [PortActionConstraint(4000)]
    public class WebhookController(ILurkBait lurkBait) : Controller
    {
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] LurkBaitTrigger lurkBaitTrigger)
        {
            await lurkBait.AwardPoints(lurkBaitTrigger);
            return Ok();
        }
    }
}
