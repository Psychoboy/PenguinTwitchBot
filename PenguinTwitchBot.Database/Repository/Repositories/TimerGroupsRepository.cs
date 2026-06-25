using PenguinTwitchBot.Database.Bot.Models.Timers;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class TimerGroupsRepository : GenericRepository<TimerGroup>, ITimerGroupsRepository
    {
        public TimerGroupsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
