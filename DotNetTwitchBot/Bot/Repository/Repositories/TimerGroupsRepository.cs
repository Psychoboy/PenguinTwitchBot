using DotNetTwitchBot.Bot.Models.Timers;

namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class TimerGroupsRepository : GenericRepository<TimerGroup>, ITimerGroupsRepository
    {
        public TimerGroupsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
