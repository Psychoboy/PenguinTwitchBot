using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IDiscordTwitchScheduleMapRepository : IGenericRepository<DiscordEventMap>
    {
    }
}
