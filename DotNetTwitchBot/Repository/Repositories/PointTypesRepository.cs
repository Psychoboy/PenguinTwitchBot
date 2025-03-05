using DotNetTwitchBot.Bot.DatabaseTools;
using DotNetTwitchBot.Bot.Models.Points;
using static LinqToDB.Reflection.Methods.LinqToDB.Insert;

namespace DotNetTwitchBot.Repository.Repositories
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
