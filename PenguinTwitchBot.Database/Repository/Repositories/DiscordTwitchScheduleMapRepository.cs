
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class DiscordTwitchScheduleMapRepository(ApplicationDbContext context) : GenericRepository<DiscordEventMap>(context), IDiscordTwitchScheduleMapRepository
    {
    }
}
