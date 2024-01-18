using DotNetTwitchBot.Bot.Models.Timers;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class TimerMessagesRepository : GenericRepository<TimerMessage>, ITimerMessagesRepository
    {
        public TimerMessagesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
