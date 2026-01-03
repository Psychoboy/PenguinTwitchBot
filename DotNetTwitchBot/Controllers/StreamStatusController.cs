using DotNetTwitchBot.Bot.Core;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [ApiController]
    public class StreamStatusController(IServiceBackbone serviceBackbone) : Controller
    {
        [HttpGet("/streamstatus")]
        public async Task<ActionResult> GetStreamStatus()
        {
            if (serviceBackbone.IsOnline)
            {
                return Ok(true);
            }
            else
            {
                return Ok(false);
            }
        }
    }
}
