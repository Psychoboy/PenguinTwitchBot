
namespace DotNetTwitchBot.Repository.Repositories
{
    public class DiscordTwitchScheduleMapRepository(ApplicationDbContext context) : GenericRepository<DiscordEventMap>(context), IDiscordTwitchScheduleMapRepository
    {
    }
}
