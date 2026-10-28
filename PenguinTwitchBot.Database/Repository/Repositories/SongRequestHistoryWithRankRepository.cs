using PenguinTwitchBot.Database.Bot.Models.Metrics;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class SongRequestHistoryWithRankRepository(ApplicationDbContext context) : GenericRepository<SongRequestHistoryWithRank>(context), ISongRequestHistoryWithRankRepository
    {
    }
}
