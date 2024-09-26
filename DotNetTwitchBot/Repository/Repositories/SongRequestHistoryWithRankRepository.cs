using DotNetTwitchBot.Bot.Models.Metrics;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class SongRequestHistoryWithRankRepository(ApplicationDbContext context) : GenericRepository<SongRequestHistoryWithRank>(context), ISongRequestHistoryWithRankRepository
    {
    }
}
