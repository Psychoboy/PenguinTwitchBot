using PenguinTwitchBot.Bot.DatabaseTools;
using PenguinTwitchBot.Bot.Models.Points;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class PointTypesRepository(ApplicationDbContext context) : 
        GenericRepository<PointType>(context), IPointTypesRepository
    {
        public override Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            IQueryable<PointType> recordsSet = context
                .Set<PointType>()
                .Include(x => x.UserPoints)
                .Include(x => x.PointCommands)
                .AsSplitQuery();
            var data = recordsSet.ToList();
            return BackupTools.WriteData<PointType>(backupDirectory, data, logger);
        }
    }
}
