using DotNetTwitchBot.Bot.Models.Points;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using System.Linq.Expressions;
using MudBlazor;
namespace DotNetTwitchBot.Repository.Repositories
{
    public class UserPointsRepository(ApplicationDbContext context) :
        GenericRepository<UserPoints>(context), IUserPointsRepository
    {
        public Task<UserPoints?> GetUserPointsByUserId(string userId, int pointType)
        {
            return _context.UserPoints.Include(x => x.PointType).FirstOrDefaultAsyncEF(x => x.UserId == userId && x.PointTypeId == pointType);
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

        public Task<UserPointsWithRank?> UserPointsByUserIdWithRank(string userId, int pointType)
        {
            var result = _context.UserPoints
                .Include(x => x.PointType)
                .Where(x => x.PointTypeId == pointType && x.Banned == false)
                .OrderByDescending(x => x.Points)
                .ToLinqToDB()
                .Select((x, i) => new UserPointsWithRank
                {
                    Id = x.Id,
                    PointTypeId = x.PointTypeId,
                    PointType = x.PointType,
                    UserId = x.UserId,
                    Username = x.Username,
                    Points = x.Points,
                    Banned = x.Banned,
                    Ranking = (int)Sql.Ext.Rank().Over().OrderByDesc(x.Points).ToValue()
                });
            var resultCte = result.AsCte();
            return resultCte.Where(x => x.UserId.Equals(userId)).FirstOrDefaultAsyncLinqToDB();
        }

        public IQueryable<UserPointsWithRank> GetRankedPoints(int pointType,
            Expression<Func<UserPointsWithRank, bool>>? filter = null, 
            Func<IQueryable<UserPointsWithRank>, IOrderedQueryable<UserPointsWithRank>>? orderBy = null, 
            int? limit = null, int? offset = null, string includeProperties = "")
        {
            var result = _context.UserPoints
            .Include(x => x.PointType)
                .Where(x => x.PointTypeId == pointType && x.Banned == false)
                .OrderByDescending(x => x.Points)
                .ToLinqToDB()
                .Select((x, i) => new UserPointsWithRank
                {
                    Id = x.Id,
                    PointTypeId = x.PointTypeId,
                    PointType = x.PointType,
                    UserId = x.UserId,
                    Username = x.Username,
                    Points = x.Points,
                    Banned = x.Banned,
                    Ranking = (int)Sql.Ext.Rank().Over().OrderByDesc(x.Points).ToValue()
                });
            var query = result.AsCte();
            if (filter != null)
            {
                query = query.Where(filter);
            }

            foreach (var includeProperty in includeProperties.Split
                (new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProperty);
            }

            if (orderBy != null)
            {
                query = orderBy(query).AsQueryable();
            }

            if (offset != null)
            {
                query = query.Skip((int)offset);
            }

            if (limit != null)
            {
                query = query.Take((int)limit);
            }
            return query;
        }
    }
}
