using PenguinTwitchBot.Bot.DatabaseTools;
using PenguinTwitchBot.Bot.Models.Wheel;

namespace PenguinTwitchBot.Repository.Repositories
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
