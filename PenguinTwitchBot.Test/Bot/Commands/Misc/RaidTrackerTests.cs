using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Database.Bot.Core;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using PenguinTwitchBot.TwitchApi.Models.Users;

namespace PenguinTwitchBot.Test.Bot.Commands.Misc
{
    public class RaidTrackerTests
    {
        [Fact]
        public async Task GetHistory_ShouldGetHistory()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var dispatcherSubstitute = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { new RaidHistoryEntry() }.AsQueryable();
            dbContext.RaidHistory.GetAllAsync().Returns(queryable);

            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, Substitute.For<ITwitchService>(), Substitute.For<IServiceBackbone>(), dispatcherSubstitute, Substitute.For<ICommandHandler>(), Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());
            //Act
            var result = await raidTracker.GetHistory();

            //Assert
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task Raid_InvalidUser_ShouldThrow()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var dispatcherSubstitute = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { new RaidHistoryEntry() }.AsQueryable();
            dbContext.RaidHistory.GetAllAsync().Returns(queryable);

            var twitchService = Substitute.For<ITwitchService>();
            twitchService.GetUserByName("").ReturnsNull();

            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, Substitute.For<IServiceBackbone>(), dispatcherSubstitute, Substitute.For<ICommandHandler>(), Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());
            //Act


            //Assert
            await Assert.ThrowsAsync<SkipCooldownException>(async () => await raidTracker.Raid(""));
        }

        [Fact]
        public async Task Raid_IsOffline_ShouldThrow()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var dispatcherSubstitute = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { new RaidHistoryEntry() }.AsQueryable();
            dbContext.RaidHistory.GetAllAsync().Returns(queryable);

            var twitchService = Substitute.For<ITwitchService>();
            twitchService.GetUserByName("").Returns(new User(Id: "", Login: "", DisplayName: "", Description: "", CreatedAt: default));
            twitchService.IsStreamOnline(Arg.Any<string>()).Returns(false);

            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, Substitute.For<IServiceBackbone>(), dispatcherSubstitute, Substitute.For<ICommandHandler>(), Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());
            //Act


            //Assert
            await Assert.ThrowsAsync<SkipCooldownException>(async () => await raidTracker.Raid(""));
        }

        [Fact]
        public async Task Raid_ShouldSucceed()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var dispatcherSubstitute = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { new RaidHistoryEntry() }.BuildMockDbSet().AsQueryable();
            dbContext.RaidHistory.Find(x => true).ReturnsForAnyArgs(queryable);

            var twitchService = Substitute.For<ITwitchService>();
            twitchService.GetUserByName("").Returns(new User(Id: "", Login: "", DisplayName: "", Description: "", CreatedAt: default));
            twitchService.IsStreamOnline(Arg.Any<string>()).Returns(true);
            twitchService.RaidStreamer(Arg.Any<string>()).Returns(true);

            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, serviceBackbone, dispatcherSubstitute, Substitute.For<ICommandHandler>(), Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());
            //Act
            await raidTracker.Raid("");

            //Assert
            dbContext.RaidHistory.Received(1).Update(Arg.Any<RaidHistoryEntry>());
            await dbContext.Received(1).SaveChangesAsync();
            await serviceBackbone.Received(1).SendChatMessage(Arg.Any<string>());
        }

        [Fact]
        public async Task Raid_WhenStartFails_DoesNotAnnounce()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var dispatcherSubstitute = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();
            var raidReward = Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var twitchService = Substitute.For<ITwitchService>();
            twitchService.GetUserByName("").Returns(new User(Id: "", Login: "", DisplayName: "", Description: "", CreatedAt: default));
            twitchService.IsStreamOnline(Arg.Any<string>()).Returns(true);
            twitchService.RaidStreamer(Arg.Any<string>()).Returns(false);

            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, serviceBackbone, dispatcherSubstitute, Substitute.For<ICommandHandler>(), raidReward);

            //Act
            await raidTracker.Raid("");

            //Assert
            await raidReward.DidNotReceive().AnnounceRaidInitiatedAsync(Arg.Any<string>());
            dbContext.RaidHistory.DidNotReceive().Update(Arg.Any<RaidHistoryEntry>());
            await serviceBackbone.DidNotReceive().SendChatMessage(Arg.Is<string>(m => m.Contains("Starting a raid")));
        }

        [Fact]
        public async Task OnIncomingRaid_NoneExisting_ShouldSucceed()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var twitchService = Substitute.For<ITwitchService>();
            var dispatcherSubstitute = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();
            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { }.BuildMockDbSet().AsQueryable();
            dbContext.RaidHistory.Find(x => true).ReturnsForAnyArgs(queryable);

            twitchService.GetUserId(Arg.Any<string>()).Returns("");
            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, serviceBackbone, dispatcherSubstitute, Substitute.For<ICommandHandler>(), Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());

            //Act
            await raidTracker.OnIncomingRaid(new PenguinTwitchBot.Bot.Events.RaidEventArgs());

            //Assert
            dbContext.RaidHistory.Received(1).Update(Arg.Any<RaidHistoryEntry>());
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task UpdateOnlineStatus_ShouldUpdateStatuses()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var twitchService = Substitute.For<ITwitchService>();
            var dispatcherSubstitute = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();
            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testRaidHistory = new RaidHistoryEntry
            {
                UserId = "123"
            };
            var queryable = new List<RaidHistoryEntry> { testRaidHistory }.BuildMockDbSet().AsQueryable();
            var emptyQueryable = new List<RaidHistoryEntry> { }.BuildMockDbSet().AsQueryable();
            dbContext.RaidHistory.Find(x => true).ReturnsForAnyArgs(queryable, emptyQueryable);

            serviceBackbone.IsOnline = true;

            twitchService.AreStreamsOnline(Arg.Any<List<string>>()).ReturnsForAnyArgs([new()]);
            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, serviceBackbone, dispatcherSubstitute, Substitute.For<ICommandHandler>(), Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());

            //Act
            await raidTracker.UpdateOnlineStatus();

            //Assert
            dbContext.RaidHistory.Received(1).UpdateRange(Arg.Any<List<RaidHistoryEntry>>());
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task OneCommand_Raid_ShouldSucceed()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var dispatcherSubstitute = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { new RaidHistoryEntry() }.BuildMockDbSet().AsQueryable();
            dbContext.RaidHistory.Find(x => true).ReturnsForAnyArgs(queryable);

            var twitchService = Substitute.For<ITwitchService>();
            twitchService.GetUserByName("").Returns(new User(Id: "", Login: "", DisplayName: "", Description: "", CreatedAt: default));
            twitchService.IsStreamOnline(Arg.Any<string>()).Returns(true);
            twitchService.RaidStreamer(Arg.Any<string>()).Returns(true);

            commandHandler.GetCommandDefaultName("raid").Returns("raid");

            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, serviceBackbone, dispatcherSubstitute, commandHandler, Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());
            //Act
            await raidTracker.OnCommand(null, new PenguinTwitchBot.Bot.Events.Chat.CommandEventArgs
            {
                TargetUser = "",
                Command = "raid"
            });

            //Assert
            dbContext.RaidHistory.Received(1).Update(Arg.Any<RaidHistoryEntry>());
            await dbContext.Received(1).SaveChangesAsync();
            await serviceBackbone.Received(1).SendChatMessage(Arg.Any<string>());
        }

        [Fact]
        public async Task PruneRaidHistory_WithOldEntries_RemovesAndReturnsCount()
        {
            // Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var oldDate = DateTime.UtcNow.AddDays(-100);
            var recentDate = DateTime.UtcNow.AddDays(-5);
            var allEntries = new List<RaidHistoryEntry>
            {
                new RaidHistoryEntry { Name = "OldStreamer1", LastIncomingRaid = oldDate, LastOutgoingRaid = DateTime.MinValue },
                new RaidHistoryEntry { Name = "OldStreamer2", LastIncomingRaid = oldDate, LastOutgoingRaid = oldDate },
                new RaidHistoryEntry { Name = "RecentIncoming", LastIncomingRaid = recentDate, LastOutgoingRaid = oldDate },
                new RaidHistoryEntry { Name = "RecentOutgoing", LastIncomingRaid = oldDate, LastOutgoingRaid = recentDate },
                new RaidHistoryEntry { Name = "RecentBoth", LastIncomingRaid = recentDate, LastOutgoingRaid = recentDate }
            };

            dbContext.RaidHistory.Find(Arg.Any<System.Linq.Expressions.Expression<Func<RaidHistoryEntry, bool>>>())
                .Returns(callInfo =>
                {
                    var predicate = callInfo.Arg<System.Linq.Expressions.Expression<Func<RaidHistoryEntry, bool>>>();
                    var filtered = allEntries.AsQueryable().Where(predicate).ToList();
                    return filtered.BuildMockDbSet().AsQueryable();
                });

            var raidTracker = new RaidTracker(
                Substitute.For<ILogger<RaidTracker>>(),
                scopeFactory,
                Substitute.For<ITwitchService>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>(),
                Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());

            // Act
            var cutoff = DateTime.UtcNow.AddDays(-30);
            var result = await raidTracker.PruneRaidHistory(cutoff);

            // Assert: only the 2 creators whose latest interaction is older than cutoff are deleted
            Assert.Equal(2, result);
        }

        [Fact]
        public async Task PruneRaidHistory_WithNoOldEntries_ReturnsZero()
        {
            // Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var recentDate = DateTime.UtcNow.AddDays(-5);
            var recentEntries = new List<RaidHistoryEntry>
            {
                new RaidHistoryEntry { Name = "RecentStreamer1", LastIncomingRaid = recentDate, LastOutgoingRaid = recentDate }
            };

            dbContext.RaidHistory.Find(Arg.Any<System.Linq.Expressions.Expression<Func<RaidHistoryEntry, bool>>>())
                .Returns(callInfo =>
                {
                    var predicate = callInfo.Arg<System.Linq.Expressions.Expression<Func<RaidHistoryEntry, bool>>>();
                    var filtered = recentEntries.AsQueryable().Where(predicate).ToList();
                    return filtered.BuildMockDbSet().AsQueryable();
                });

            var raidTracker = new RaidTracker(
                Substitute.For<ILogger<RaidTracker>>(),
                scopeFactory,
                Substitute.For<ITwitchService>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>(),
                Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());

            // Act
            var cutoff = DateTime.UtcNow.AddDays(-30);
            var result = await raidTracker.PruneRaidHistory(cutoff);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task PruneRaidHistory_WhenDatabaseThrows_LogsAndRethrows()
        {
            // Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            dbContext.RaidHistory.Find(Arg.Any<System.Linq.Expressions.Expression<Func<RaidHistoryEntry, bool>>>())
                .Throws(new InvalidOperationException("Database connection failed"));

            var raidTracker = new RaidTracker(
                Substitute.For<ILogger<RaidTracker>>(),
                scopeFactory,
                Substitute.For<ITwitchService>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>(),
                Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => raidTracker.PruneRaidHistory(DateTime.UtcNow.AddDays(-30)));
        }
    }
}
