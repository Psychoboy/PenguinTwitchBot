
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class DiscordTwitchScheduleMapRepository(ApplicationDbContext context) : GenericRepository<DiscordEventMap>(context), IDiscordTwitchScheduleMapRepository
    {
    }
}
