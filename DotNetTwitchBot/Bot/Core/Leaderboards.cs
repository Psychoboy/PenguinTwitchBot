using DotNetTwitchBot.Bot.Repository;
using DotNetTwitchBot.Models;

namespace DotNetTwitchBot.Bot.Core
{
    public class Leaderboards
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public Leaderboards(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<PagedDataResponse<LeaderPosition>> GetPasties(PaginationFilter filter)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var validFilter = new PaginationFilter(filter.Page, filter.Count);
            var pagedData = await unitOfWork.ViewerPointWithRanks.GetAsync(
                filter: string.IsNullOrWhiteSpace(filter.Filter) ? null : x => x.Username.Contains(filter.Filter),
                orderBy: a => a.OrderBy(b => b.Ranking),
                offset: (validFilter.Page) * filter.Count,
                limit: filter.Count
                );
            var totalRecords = await unitOfWork.ViewerPointWithRanks.CountAsync();

            return new PagedDataResponse<LeaderPosition>
            {
                Data = pagedData.Select(x => new LeaderPosition { Rank = x.Ranking, Amount = x.Points, Name = x.Username }).ToList(),
                TotalItems = totalRecords
            };
        }

        public async Task<PagedDataResponse<LeaderPosition>> GetTickets(PaginationFilter filter)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var validFilter = new PaginationFilter(filter.Page, filter.Count);
            var pagedData = await unitOfWork.ViewerTicketsWithRank.GetAsync(
                filter: string.IsNullOrWhiteSpace(filter.Filter) ? null : x => x.Username.Contains(filter.Filter),
                orderBy: a => a.OrderBy(b => b.Ranking),
                offset: (validFilter.Page) * filter.Count,
                limit: filter.Count
                );
            var totalRecords = await unitOfWork.ViewerTicketsWithRank.CountAsync();

            return new PagedDataResponse<LeaderPosition>
            {
                Data = pagedData.Select(x => new LeaderPosition { Rank = x.Ranking, Amount = x.Points, Name = x.Username }).ToList(),
                TotalItems = totalRecords
            };
        }

        public async Task<PagedDataResponse<LeaderPosition>> GetLoudest(PaginationFilter filter)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var validFilter = new PaginationFilter(filter.Page, filter.Count);
            var pagedData = await unitOfWork.ViewerMessageCountsWithRank.GetAsync(
                filter: string.IsNullOrWhiteSpace(filter.Filter) ? null : x => x.Username.Contains(filter.Filter),
                orderBy: a => a.OrderBy(b => b.Ranking),
                offset: (validFilter.Page) * filter.Count,
                limit: filter.Count
                );
            var totalRecords = await unitOfWork.ViewerMessageCountsWithRank.CountAsync();

            return new PagedDataResponse<LeaderPosition>
            {
                Data = pagedData.Select(x => new LeaderPosition { Rank = x.Ranking, Amount = x.MessageCount, Name = x.Username }).ToList(),
                TotalItems = totalRecords
            };
        }

        public async Task<PagedDataResponse<LeaderPosition>> GetTime(PaginationFilter filter)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var validFilter = new PaginationFilter(filter.Page, filter.Count);
            var pagedData = await unitOfWork.ViewersTimeWithRank.GetAsync(
                filter: string.IsNullOrWhiteSpace(filter.Filter) ? null : x => x.Username.Contains(filter.Filter),
                orderBy: a => a.OrderBy(b => b.Ranking),
                offset: (validFilter.Page) * filter.Count,
                limit: filter.Count
                );
            var totalRecords = await unitOfWork.ViewersTimeWithRank.CountAsync();

            return new PagedDataResponse<LeaderPosition>
            {
                Data = pagedData.Select(x => new LeaderPosition { Rank = x.Ranking, Amount = x.Time, Name = x.Username }).ToList(),
                TotalItems = totalRecords
            };
        }
    }
}
