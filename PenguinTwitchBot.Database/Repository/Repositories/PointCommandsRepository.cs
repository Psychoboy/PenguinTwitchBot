using PenguinTwitchBot.Bot.Models.Points;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class PointCommandsRepository(ApplicationDbContext context) : GenericRepository<PointCommand>(context), IPointCommandsRepository
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
