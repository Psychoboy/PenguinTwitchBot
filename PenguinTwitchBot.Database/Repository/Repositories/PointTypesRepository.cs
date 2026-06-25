
using PenguinTwitchBot.Database.Bot.DatabaseTools;
using PenguinTwitchBot.Database.Bot.Models.Points;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class PointTypesRepository(ApplicationDbContext context) : 
        GenericRepository<PointType>(context), IPointTypesRepository
    {
        public override async Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            IQueryable<PointType> recordsSet = context
                .Set<PointType>()
                .AsNoTracking()
                .Include(x => x.UserPoints)
                .Include(x => x.PointCommands)
                .AsSplitQuery();
            var data = await recordsSet.ToListAsync();
            await BackupTools.WriteData<PointType>(backupDirectory, data, logger);
        }
    }
}
