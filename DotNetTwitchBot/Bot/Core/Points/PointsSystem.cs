using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Commands.TicketGames;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Points;
using DotNetTwitchBot.Models;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public class PointsSystem(
        IViewerFeature viewerFeature,
        ILogger<PointsSystem> logger,
        IServiceScopeFactory scopeFactory,
        IGameSettingsService gameSettingsService,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler

        ) : BaseCommandService(serviceBackbone, commandHandler, "PointsSystem"), IPointsSystem, IHostedService
    {
        public static Int64 MaxBet { get; } = 200000069;
        public static bool IncludeSubsInActive = true;
        private static readonly Prometheus.Gauge NumberOfPoints = Prometheus.Metrics.CreateGauge("points", "Number of points", new string[] { "username", "pointTypeId" });
        private static readonly Prometheus.Gauge NumberOfPointsByGame = Prometheus.Metrics.CreateGauge("points_by_game", "Number of points by game", new string[] { "game", "pointTypeId" });
        public async Task<long> AddPointsByUserId(string userId, int pointType, long points)
        {
            try
            {
                var viewer = await viewerFeature.GetViewerByUserId(userId);
                if (viewer == null)
                {
                    logger.LogWarning("Viewer not found for user {userId}", userId);
                    return 0;
                }
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var userPoints = await db.UserPoints.GetUserPointsByUserId(userId, pointType);
                if (userPoints == null)
                {
                    userPoints = new UserPoints
                    {
                        UserId = userId,
                        Username = viewer.Username,
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
                NumberOfPoints.WithLabels(userPoints.Username, pointType.ToString()).Inc(points);
                await db.SaveChangesAsync();
                return userPoints.Points;
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Error adding points to user {userId}", userId);
                return 0;
            }
        }

        public async Task<long> AddPointsByUsername(string username, int pointType, long points)
        {
            var viewer = await viewerFeature.GetViewerByUserName(username);
            if (viewer == null)
            {
                logger.LogWarning("Viewer not found for username {username}", username.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", ""));
                return 0;
            }
            return await AddPointsByUserId(viewer.UserId, pointType, points);
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
            var viewer = await viewerFeature.GetViewerByUserId(userId);
            if (viewer == null)
            {
                logger.LogWarning("Viewer not found for user {userId}", userId);
                return new();
            }
            if (userPoints == null)
            {
                userPoints = new UserPoints
                {
                    UserId = userId,
                    Username = viewer.Username,
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
            await SetupSpecialServices();
            await Register();
            ServiceBackbone.StreamStarted += StreamStarted;
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

        private async Task SetupSpecialServices()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var bonusService = scope.ServiceProvider.GetRequiredService<IBonusTickets>();
            await bonusService.Setup();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {module}", nameof(PointsSystem));
            ServiceBackbone.StreamStarted -= StreamStarted;
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
            return await db.PointTypes.GetAsync(includeProperties: "PointCommands");
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

            var defaultPoint = new PointType
            {
                Id = 1,
                Name = "Points",
                Description = "Points earned by viewers",
            };

            defaultPoint.PointCommands.Add(new PointCommand
            {
                CommandName = "addpoints",
                Description = "Add points to a user",
                MinimumRank = Rank.Streamer,
                SayCooldown = false,
                Category = "Points",
                CommandType = PointCommandType.Add
            });

            defaultPoint.PointCommands.Add(new PointCommand
            {
                CommandName = "removepoints",
                Description = "Remove points from a user",
                MinimumRank = Rank.Streamer,
                SayCooldown = false,
                Category = "Points",
                CommandType = PointCommandType.Remove
            });

            defaultPoint.PointCommands.Add(new PointCommand
            {
                CommandName = "getpoints",
                Description = "Get points for a user",
                MinimumRank = Rank.Viewer,
                SayCooldown = false,
                Category = "Points",
                CommandType = PointCommandType.Get
            });

            defaultPoint.PointCommands.Add(new PointCommand
            {
                CommandName = "setpoints",
                Description = "Set points for a user",
                MinimumRank = Rank.Streamer,
                SayCooldown = false,
                Category = "Points",
                CommandType = PointCommandType.Set
            });

            defaultPoint.PointCommands.Add(new PointCommand
            {
                CommandName = "addactivepoints",
                Description = "Add points to active users",
                MinimumRank = Rank.Streamer,
                SayCooldown = false,
                Category = "Points",
                CommandType = PointCommandType.AddActive
            });
            return defaultPoint;
        }

        public Task<PointType> GetPointTypeForGame(string gameName)
        {
            return gameSettingsService.GetPointTypeForGame(gameName);
        }

        public Task RegisterDefaultPointForGame(string gameName)
        {
            return gameSettingsService.RegisterDefaultPointForGame(gameName);
        }

        public Task SetPointTypeForGame(string gameName, int pointTypeId)
        {
            return gameSettingsService.SetPointTypeForGame(gameName, pointTypeId);
        }

        public async Task<bool> RemovePointsFromUserByUserId(string userId, int pointType, long points)
        {
            var userPoints = await GetUserPointsByUserId(userId, pointType);
            if (userPoints == null)
            {
                return false;
            }
            if (userPoints.Points < points)
            {
                return false;
            }
            userPoints.Points -= points;
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.UserPoints.Update(userPoints);
            await db.SaveChangesAsync();
            NumberOfPoints.WithLabels(userPoints.Username, pointType.ToString()).Dec(points);
            return true;
        }

        public async Task<bool> RemovePointsFromUserByUsername(string username, int pointType, long points)
        {
            var viewer = await viewerFeature.GetViewerByUserName(username);
            if (viewer == null)
            {
                logger.LogWarning("Viewer not found for username {username}", username);
                return false;
            }
            return await RemovePointsFromUserByUserId(viewer.UserId, pointType, points);
        }

        public async Task<UserPoints> GetUserPointsByUserIdAndGame(string userId, string gameName)
        {
            var pointType = await GetPointTypeForGame(gameName);
            return await GetUserPointsByUserId(userId, pointType.GetId());
        }

        public async Task<UserPoints> GetUserPointsByUsernameAndGame(string username, string gameName)
        {
            var pointType = await GetPointTypeForGame(gameName);
            return await GetUserPointsByUsername(username, pointType.GetId());
        }

        public async Task<bool> RemovePointsFromUserByUserIdAndGame(string userId, string gameName, long points)
        {
            var pointType = await GetPointTypeForGame(gameName);
            NumberOfPointsByGame.WithLabels(gameName.ToLower(), pointType.GetId().ToString()).Dec(points);
            return await RemovePointsFromUserByUserId(userId, pointType.GetId(), points);
        }

        public async Task<bool> RemovePointsFromUserByUsernameAndGame(string username, string gameName, long points)
        {
            var pointType = await GetPointTypeForGame(gameName);
            NumberOfPointsByGame.WithLabels(gameName.ToLower(), pointType.GetId().ToString()).Dec(points);
            return await RemovePointsFromUserByUsername(username, pointType.GetId(), points);
        }

        public async Task<long> AddPointsByUserIdAndGame(string userId, string gameName, long points)
        {
            var pointType = await GetPointTypeForGame(gameName);
            NumberOfPointsByGame.WithLabels(gameName.ToLower(), pointType.GetId().ToString()).Inc(points);
            return await AddPointsByUserId(userId, pointType.GetId(), points);
        }

        public async Task<long> AddPointsByUsernameAndGame(string username, string gameName, long points)
        {
            var pointType = await GetPointTypeForGame(gameName);
            NumberOfPointsByGame.WithLabels(gameName.ToLower(), pointType.GetId().ToString()).Inc(points);
            return await AddPointsByUsername(username, pointType.GetId(), points);
        }

        public async Task<long> GetMaxPointsByUserIdAndGame(string userId, string gameName, long max)
        {
            var pointType = await GetPointTypeForGame(gameName);
            return await GetMaxPointsByUserId(userId, pointType.GetId(), max);
        }

        public async Task<PointCommand?> GetPointCommand(string pointTypeCommand)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return (await db.PointCommands.GetAsync(x => x.CommandName.Equals(pointTypeCommand), includeProperties: "PointType")).FirstOrDefault();
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            switch (command)
            {
                case "loyalty":
                    { 
                        await SayLoyalty(e);
                    }
                    break;
            }
        }

        private async Task SayLoyalty(CommandEventArgs e)
        {
            var loyaltyMessage = await gameSettingsService.GetStringSetting(
                "loyalty",
                "LoyaltyMessage",
                "{NameWithTitle} Watch Time: [{WatchTime}] - {PointsName}: [#{PointsRank}, {Points} - Messages: [#{MessagesRank}, {Messages}]");
            var loyaltyPointType = await GetPointTypeForGame("loyalty");
            var points = await GetPointsWithRankByUserId(e.UserId, loyaltyPointType.GetId());
            var time = await GetUserTimeAndRank(e.Name);
            var messages = await GetUserMessagesAndRank(e.Name);
            loyaltyMessage = loyaltyMessage
                .Replace("{NameWithTitle}", await viewerFeature.GetNameWithTitle(e.Name), StringComparison.OrdinalIgnoreCase)
                .Replace("{WatchTime}", StaticTools.ConvertToCompoundDuration(time.Time), StringComparison.OrdinalIgnoreCase)
                .Replace("{PointsName}", loyaltyPointType.Name, StringComparison.OrdinalIgnoreCase)
                .Replace("{PointsRank}", points.Ranking.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{Points}", points.Points.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                .Replace("{MessagesRank}", messages.Ranking.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{Messages}", messages.MessageCount.ToString("N0"), StringComparison.OrdinalIgnoreCase);
            await SendChatMessage(e.Name, loyaltyMessage);
        }

        public async Task<ViewerTimeWithRank> GetUserTimeAndRank(string name)
        {
            ViewerTimeWithRank? viewerTime;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerTime = await db.ViewersTimeWithRank.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            }
            return viewerTime ?? new ViewerTimeWithRank() { Ranking = int.MaxValue };
        }

        public async Task<ViewerMessageCountWithRank> GetUserMessagesAndRank(string name)
        {
            ViewerMessageCountWithRank? viewerMessage;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerMessage = await db.ViewerMessageCountsWithRank.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            }

            return viewerMessage ?? new ViewerMessageCountWithRank { Ranking = int.MaxValue };
        }

        public override async Task Register()
        {
            await RegisterDefaultCommand("loyalty", this, ModuleName);
            logger.LogInformation("Registered commands for {module}", ModuleName);
        }

        public Task<List<PointGamePair>> GetPointTypesForGames()
        {
            return gameSettingsService.GetAllPointTypes();
        }

        public async Task RunCommand(CommandEventArgs e, PointCommand pointCommand)
        {
            switch (pointCommand.CommandType)
            {
                case PointCommandType.Add:
                    {
                        if (Int64.TryParse(e.Args[1], out long amount))
                        {
                            var userId = await viewerFeature.GetViewerId(e.TargetUser);
                            if (userId == null) return;
                            await AddPointsByUsername(e.TargetUser, pointCommand.PointType.GetId(), amount);
                            var userPoints = await GetUserPointsByUsername(e.TargetUser, pointCommand.PointType.GetId());    
                            await SendChatMessage(e.TargetUser, $"Gave you {amount:N0} {pointCommand.PointType.Name}, you now have {userPoints.Points:N0} {pointCommand.PointType.Name}");
                            
                            logger.LogInformation("Added {amount} {pointType} to {username}", amount, pointCommand.PointType.Name, e.TargetUser);

                        }
                        break;
                    }
                case PointCommandType.Remove:
                    {
                        if (Int64.TryParse(e.Args[1], out long amount))
                        {
                            var userId = await viewerFeature.GetViewerId(e.TargetUser);
                            if (userId == null) return;
                            await RemovePointsFromUserByUsername(e.Name, pointCommand.PointType.GetId(), amount);
                            logger.LogInformation("Removed {amount} {pointType} from {username}", amount, pointCommand.PointType.Name, e.TargetUser);
                        }
                        break;
                    }
                case PointCommandType.Get:
                    //var userPoints = await GetUserPointsByUsername(e.Username, pointCommand.PointType.GetId());
                    //if (userPoints != null)
                    //{
                    //    await SendChatMessage(e.Username, $"You have {userPoints.Points} {pointCommand.PointType.Name}");
                    //}
                    await SendPointsMessage(e, pointCommand.PointType.GetId());
                    break;
                case PointCommandType.Set:
                    break;
                case PointCommandType.AddActive:
                    {
                        if (Int64.TryParse(e.Args[1], out long amount))
                        {
                            await AddPointsToActiveUsers(pointCommand.PointType.GetId(), amount);
                        }
                        break;
                    }
            }
        }

        public async Task AddPointsToActiveUsers(int pointType, long points)
        {
            var activeViewers = viewerFeature.GetActiveViewers();
            if (IncludeSubsInActive)
            {
                var onlineViewers = viewerFeature.GetCurrentViewers();
                foreach (var viewer in onlineViewers)
                {
                    if (!activeViewers.Contains(viewer) && await viewerFeature.IsSubscriber(viewer))
                    {
                        activeViewers.Add(viewer);
                    }
                }
            }
            viewerFeature.GetActiveViewers().Distinct().ToList().ForEach(async viewer =>
            {
                await AddPointsByUsername(viewer, pointType, points);
            });
        }

        public Task AddPointsToSubbedUsers(int pointType, long points)
        {
            viewerFeature.GetCurrentViewers().ForEach(async viewer =>
            {
                if (await viewerFeature.IsSubscriber(viewer))
                {
                    await AddPointsByUsername(viewer, pointType, points);
                }
            });
            return Task.CompletedTask;
        }

        public Task AddPointsToAllCurrentUsers(int pointType, long points)
        {
            viewerFeature.GetCurrentViewers().ForEach(async viewer =>
            {
                await AddPointsByUsername(viewer, pointType, points);
            });
            return Task.CompletedTask;
        }

        public async Task<UserPointsWithRank> GetPointsWithRankByUsername(string name, int pointType)
        {
            var viewer = await viewerFeature.GetViewerByUserName(name);
            if (viewer == null)
            {
                logger.LogWarning("Viewer not found for username {username}", name);
                return new UserPointsWithRank
                {
                    UserId = "",
                    Points = 0,
                    Ranking = int.MaxValue,
                    Username = ""
                };
            }
            return await GetPointsWithRankByUserId(name, pointType);
        }

        public async Task<UserPointsWithRank> GetPointsWithRankByUserId(string userId, int pointType)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var userPoints = await db.UserPoints.UserPointsByUserIdWithRank(userId, pointType);
            var userPointType = await db.PointTypes.GetByIdAsync(pointType);
            return userPoints ?? new UserPointsWithRank
            {
                UserId = userId,
                Points = 0,
                Ranking = int.MaxValue,
                Username = "",
                PointType = userPointType ?? new PointType()
            };

        }

        private async Task SendPointsMessage(CommandEventArgs e, int pointType)
        {
            var userId = e.UserId;
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var userPoints = await db.UserPoints.UserPointsByUserIdWithRank(userId, pointType);
            var userPointType = await db.PointTypes.GetByIdAsync(pointType);
            if (userPoints != null)
            {
                await SendChatMessage(e.Name, $"You are ranked #{userPoints.Ranking} and have {userPoints.Points:N0} {userPointType?.Name}");
            }
        }

        public async Task<PagedDataResponse<LeaderPosition>> GetLeaderPositions(PaginationFilter filter, int pointType)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var validFilter = new PaginationFilter(filter.Page, filter.Count);
            var pagedData = db.UserPoints.GetRankedPoints(pointType,
                filter: string.IsNullOrWhiteSpace(filter.Filter) ? null : x => x.Username.Contains(filter.Filter),
                offset: (validFilter.Page) * filter.Count,
                limit: filter.Count);

            var totalRecords = 0;
            if (string.IsNullOrWhiteSpace(filter.Filter))
            {
                totalRecords = await db.UserPoints.Find(x => x.PointTypeId == pointType).CountAsync();
            }
            else
            {
                totalRecords = await db.UserPoints.Find(x => x.Username.Contains(filter.Filter) && x.PointTypeId == pointType).CountAsync();
            }
            return new PagedDataResponse<LeaderPosition>
            {
                Data = pagedData.Select(x => new LeaderPosition { Rank = x.Ranking, Amount = x.Points, Name = x.Username }).ToList(),
                TotalItems = totalRecords
            };
        }

        public async Task RemoveAllPointsForGame(string gameName)
        {
            var pointType = await GetPointTypeForGame(gameName);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var points = await db.UserPoints.Find(x => x.PointTypeId == pointType.Id).ToListAsync();
            db.UserPoints.RemoveRange(points);
            await db.SaveChangesAsync();
            NumberOfPointsByGame.WithLabels(gameName.ToLower(), pointType.GetId().ToString()).Set(0);
        }

        private Task StreamStarted(object? sender, EventArgs _)
        {
            {
                var labels = NumberOfPoints.GetAllLabelValues();
                foreach (var label in labels)
                {
                    NumberOfPoints.RemoveLabelled(label);
                }
            }
            {
                var labels = NumberOfPointsByGame.GetAllLabelValues();
                foreach (var label in labels)
                {
                    NumberOfPointsByGame.RemoveLabelled(label);
                }
            }


            return Task.CompletedTask;
        }
    }
}
