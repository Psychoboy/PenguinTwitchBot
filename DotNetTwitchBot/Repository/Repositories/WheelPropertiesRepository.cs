using DotNetTwitchBot.Bot.DatabaseTools;
using DotNetTwitchBot.Bot.Models.Wheel;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class WheelPropertiesRepository(ApplicationDbContext context) : GenericRepository<WheelProperty>(context), IWheelPropertiesRepository
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
