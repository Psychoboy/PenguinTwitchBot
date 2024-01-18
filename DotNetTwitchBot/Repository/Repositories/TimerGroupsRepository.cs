using DotNetTwitchBot.Bot.Models.Timers;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class TimerGroupsRepository : GenericRepository<TimerGroup>, ITimerGroupsRepository
    {
        public TimerGroupsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
