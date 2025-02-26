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

        public IAsyncEnumerable<UserPointsWithRank> UserPointsWithRank(int pointType)
        {
            return _context.UserPoints
                .Include(x => x.PointType)
                .Where(x => x.PointTypeId == pointType)
                .OrderByDescending(x => x.Points)
                .AsAsyncEnumerable()
                .Select((x, i) => new UserPointsWithRank
                {
                    Id = x.Id,
                    PointTypeId = x.PointTypeId,
                    PointType = x.PointType,
                    UserId = x.UserId,
                    Username = x.Username,
                    Points = x.Points,
                    Banned = x.Banned,
                    Ranking = i + 1
                });
        }

        public ValueTask<UserPointsWithRank?> UserPointsByUserIdWithRank(string userId, int pointType)
        {

            return _context.UserPoints
                .Include(x => x.PointType)
                .Where(x => x.PointTypeId == pointType && x.Banned == false)
                .OrderByDescending(x => x.Points)
                .AsAsyncEnumerable()
                .Select((x, i) => new UserPointsWithRank
                {
                    Id = x.Id,
                    PointTypeId = x.PointTypeId,
                    PointType = x.PointType,
                    UserId = x.UserId,
                    Username = x.Username,
                    Points = x.Points,
                    Banned = x.Banned,
                    Ranking = i + 1
                }).Where(x => x.UserId.Equals(userId)).FirstOrDefaultAsync();
        }
    }
}
