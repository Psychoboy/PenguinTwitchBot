using PenguinTwitchBot.Bot.Models.Timers;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class TimerGroupsRepository : GenericRepository<TimerGroup>, ITimerGroupsRepository
    {
        public TimerGroupsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
