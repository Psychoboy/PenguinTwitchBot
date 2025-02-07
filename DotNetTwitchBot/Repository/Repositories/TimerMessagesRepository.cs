using DotNetTwitchBot.Bot.Models.Timers;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class TimerMessagesRepository(ApplicationDbContext context) : GenericRepository<TimerMessage>(context), ITimerMessagesRepository
    {
        public override Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            return Task.CompletedTask;
        }

        public override Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            return Task.CompletedTask;
        }
    }
}
