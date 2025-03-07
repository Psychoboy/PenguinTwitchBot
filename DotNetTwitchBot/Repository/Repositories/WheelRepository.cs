using DotNetTwitchBot.Bot.Models.Wheel;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class WheelRepository(ApplicationDbContext context) : GenericRepository<Wheel>(context), IWheelRepository
    {
        public override Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            context.Set<WheelProperty>().ExecuteDelete();
            return base.RestoreTable(context, backupDirectory, logger);
        }
    }
}
