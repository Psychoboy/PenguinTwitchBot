using DotNetTwitchBot.Bot.Models.Points;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class UserPointsRepository(ApplicationDbContext context) :
        GenericRepository<UserPoints>(context), IUserPointsRepository
    {
        public Task<UserPoints?> GetUserPointsByUserId(string userId, int pointType)
        {
            return _context.UserPoints.Include(x => x.PointType).FirstOrDefaultAsync(x => x.UserId == userId && x.PointTypeId == pointType);
        }

        // Override the backup and restore methods to do nothing, they are covered by PointTypesRepository
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
