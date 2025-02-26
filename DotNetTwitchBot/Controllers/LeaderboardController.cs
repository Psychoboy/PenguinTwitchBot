using DotNetTwitchBot.Models;
using DotNetTwitchBot.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class LeaderboardController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<LeaderboardController> _logger;

        public LeaderboardController(IUnitOfWork unitOfWork, ILogger<LeaderboardController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;

        }
    }
}
