namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class ViewerMessageCountsWithRankRepository : GenericRepository<ViewerMessageCountWithRank>, IViewerMessageCountsWithRankRepository
    {
        public ViewerMessageCountsWithRankRepository(ApplicationDbContext context) : base(context)
        {
        }

        public Task<List<ViewerMessageCountWithRank>> GetTopN(int count)
        {
            return _context.ViewerMessageCountWithRanks.OrderBy(x => x.Ranking).Take(count).ToListAsync();
        }
    }
}
