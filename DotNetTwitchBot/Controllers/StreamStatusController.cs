using DotNetTwitchBot.Bot.Core;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [ApiController]
    public class StreamStatusController(IServiceBackbone serviceBackbone) : Controller
    {
        [HttpGet("/streamstatus")]
        public ActionResult GetStreamStatus()
        {
            return Ok(serviceBackbone.IsOnline);
        }
    }
}
