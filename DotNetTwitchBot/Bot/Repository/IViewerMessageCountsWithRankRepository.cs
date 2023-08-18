namespace DotNetTwitchBot.Bot.Repository
{
    public interface IViewerMessageCountsWithRankRepository : IGenericRepository<ViewerMessageCountWithRank>
    {
        Task<List<ViewerMessageCountWithRank>> GetTopN(int count);
    }
}
