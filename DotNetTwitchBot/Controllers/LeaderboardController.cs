using DotNetTwitchBot.Repository;
using DotNetTwitchBot.Models;
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

        public LeaderboardController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

        }
        [HttpGet("/pasties")]
        public async Task<IActionResult> Get([FromQuery] PaginationFilter filter)
        {
            var validFilter = new PaginationFilter(filter.Page, filter.Count);
            var pagedData = await _unitOfWork.ViewerPointWithRanks.GetAsync(
                filter: string.IsNullOrWhiteSpace(filter.Filter) ? null : x => x.Username.Equals(filter.Filter),
                orderBy: a => a.OrderBy(b => b.Ranking),
                offset: (validFilter.Page - 1) * filter.Count,
                limit: filter.Count
                );
            var totalRecords = await _unitOfWork.ViewerPointWithRanks.CountAsync();
            return Ok(new PagedDataResponse<LeaderPosition>
            {
                Data = pagedData.Select(x => new LeaderPosition { Rank = x.Ranking, Amount = x.Points, Name = x.Username }).ToList(),
                TotalItems = totalRecords
            });
        }
    }
}
