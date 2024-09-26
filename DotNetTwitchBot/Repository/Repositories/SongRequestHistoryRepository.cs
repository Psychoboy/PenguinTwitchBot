using DotNetTwitchBot.Bot.Models.Metrics;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class SongRequestHistoryRepository(ApplicationDbContext context) : GenericRepository<SongRequestHistory>(context), ISongRequestHistoryRepository
    {
    }
}
