using PenguinTwitchBot.Bot.Models.Wheel;

namespace PenguinTwitchBot.Repository.Repositories
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
