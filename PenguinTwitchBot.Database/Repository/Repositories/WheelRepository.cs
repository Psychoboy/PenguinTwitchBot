using PenguinTwitchBot.Database.Bot.Models.Wheel;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
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
