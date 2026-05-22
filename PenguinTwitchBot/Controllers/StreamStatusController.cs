using PenguinTwitchBot.Bot.Core;
using Microsoft.AspNetCore.Mvc;

namespace PenguinTwitchBot.Controllers
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
