
using PenguinTwitchBot.Database.Bot.Models.Wheel;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
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
