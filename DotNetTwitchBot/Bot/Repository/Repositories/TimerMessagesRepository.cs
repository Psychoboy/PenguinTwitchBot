using DotNetTwitchBot.Bot.Models.Timers;

namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class TimerMessagesRepository : GenericRepository<TimerMessage>, ITimerMessagesRepository
    {
        public TimerMessagesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
