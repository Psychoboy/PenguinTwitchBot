using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Models.Points;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public class PointsSystem(
        IViewerFeature viewerFeature,
        ILogger<PointsSystem> logger,
        IServiceScopeFactory scopeFactory
        ) : IPointsSystem, IHostedService
    {
        public async Task AddPointsByUserId(string userId, int pointType, long points)
        {
            try
            {
                var viewer = await viewerFeature.GetViewerByUserId(userId);
                if (viewer == null)
                {
                    logger.LogWarning("Viewer not found for user {userId}", userId);
                    return;
                }
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var userPoints = await db.UserPoints.GetUserPointsByUserId(userId, pointType);
                if (userPoints == null)
                {
                    userPoints = new UserPoints
                    {
                        UserId = userId,
                        PointTypeId = pointType,
                        Points = points
                    };
                    await db.UserPoints.AddAsync(userPoints);
                }
                else
                {
                    userPoints.Points += points;
                    db.UserPoints.Update(userPoints);
                }
                await db.SaveChangesAsync();
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Error adding points to user {userId}", userId);
            }
        }

        public async Task AddPointsByUsername(string username, int pointType, long points)
        {
            var viewer = await viewerFeature.GetViewerByUserName(username);
            if (viewer == null)
            {
                logger.LogWarning("Viewer not found for username {username}", username);
                return;
            }
            await AddPointsByUserId(viewer.UserId, pointType, points);
        }

        //public Task<long> GetMaxPointsByUserId(string userId, int pointType)
        //{
        //    throw new NotImplementedException();
        //}

        public async Task<long> GetMaxPointsByUserId(string userId, int pointType, long max)
        {
            var viewerPoints = await GetUserPointsByUserId(userId, pointType);
            if (viewerPoints == null)
            {
                return 0;
            }
            if(viewerPoints.Points > max)
            {
                return max;
            }
            return viewerPoints.Points;
        }

        public async Task<UserPoints> GetUserPointsByUserId(string userId, int pointType)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var userPoints = await db.UserPoints.GetUserPointsByUserId(userId, pointType);
            if (userPoints == null)
            {
                userPoints = new UserPoints
                {
                    UserId = userId,
                    PointTypeId = pointType
                };
                await db.UserPoints.AddAsync(userPoints);
                await db.SaveChangesAsync();
            }
            return userPoints;
        }

        public async Task<UserPoints> GetUserPointsByUsername(string username, int pointType)
        {
            var viewer = await viewerFeature.GetViewerByUserName(username);
            if (viewer == null)
            {
                logger.LogWarning("Viewer not found for username {username}", username);
                return new();
            }
            return await GetUserPointsByUserId(viewer.UserId, pointType);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await CreateInitialDataIfNeeded();
            logger.LogInformation("Started {module}", nameof(PointsSystem));
        }

        private async Task CreateInitialDataIfNeeded()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var pointTypes = await db.PointTypes.GetAllAsync();
            if (!pointTypes.Any())
            {
                await db.PointTypes.AddAsync(GetDefaultPointType());
                await db.SaveChangesAsync();
                logger.LogInformation("Initial data created");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {module}", nameof(PointsSystem));
            return Task.CompletedTask;
        }

        public async Task<PointType?> GetPointTypeById(int pointTypeId)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.PointTypes.GetByIdAsync(pointTypeId);

        }

        public async Task<IEnumerable<PointType>> GetPointTypes()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.PointTypes.GetAllAsync();
        }

        public async Task AddPointType(PointType pointType)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.PointTypes.AddAsync(pointType);
            await db.SaveChangesAsync();
        }

        public async Task UpdatePointType(PointType pointType)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.PointTypes.Update(pointType);
            await db.SaveChangesAsync();
        }

        public async Task DeletePointType(int pointTypeId)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var pointType = await db.PointTypes.GetByIdAsync(pointTypeId);
            if (pointType != null)
            {
                db.PointTypes.Remove(pointType);
                await db.SaveChangesAsync();
            }
        }

        public static PointType GetDefaultPointType()
        {
            return new PointType
            {
                Id = 1,
                Name = "Points",
                Description = "Points earned by viewers",
                AddCommand = "addpoints",
                RemoveCommand = "removepoints",
                GetCommand = "points",
                SetCommand = "setpoints",
                AddActiveCommand = "addactivepoints"
            };
        }
    }
}
