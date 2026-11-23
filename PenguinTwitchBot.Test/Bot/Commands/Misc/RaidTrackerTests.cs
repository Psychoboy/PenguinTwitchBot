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

        [Fact]
        public async Task GetDisplayHistoryAsync_WhenIncludeLiveTeamMembersIsFalse_DoesNotQueryTeamMembers()
        {
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var twitchService = Substitute.For<ITwitchService>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            dbContext.RaidHistory.GetAllAsync().Returns(new List<RaidHistoryEntry>
            {
                new() { Id = Guid.NewGuid(), UserId = "u1", Name = "alice", DisplayName = "Alice", IsOnline = true, TotalIncomingRaids = 2 }
            });

            var raidTracker = new RaidTracker(
                Substitute.For<ILogger<RaidTracker>>(),
                scopeFactory,
                twitchService,
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>(),
                Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());

            var result = await raidTracker.GetDisplayHistoryAsync(includeLiveTeamMembers: false);

            var item = Assert.Single(result);
            Assert.Equal("u1", item.UserId);
            Assert.False(item.IsStreamTeamMember);
            Assert.True(item.IsHistoryEntry);
            await twitchService.DidNotReceive().GetStreamTeamMembersAsync();
        }

        [Fact]
        public async Task GetDisplayHistoryAsync_WhenIncludeLiveTeamMembersIsTrue_MarksExistingAndAddsLiveOnlyTeamMembers()
        {
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var twitchService = Substitute.For<ITwitchService>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            dbContext.RaidHistory.GetAllAsync().Returns(new List<RaidHistoryEntry>
            {
                new() { Id = Guid.NewGuid(), UserId = "u1", Name = "alice", DisplayName = "Alice", IsOnline = false, TotalIncomingRaids = 3, LastGame = "Old Game" },
                new() { Id = Guid.NewGuid(), UserId = "u9", Name = "nonteam", DisplayName = "NonTeam", IsOnline = true, TotalIncomingRaids = 1, LastGame = "Chess" }
            });

            twitchService.GetStreamTeamMembersAsync().Returns(new List<PenguinTwitchBot.Bot.TwitchServices.TwitchModels.StreamTeamMember>
            {
                new() { UserId = "u1", UserLogin = "alice", DisplayName = "Alice", TeamNames = ["Penguin Squad"] },
                new() { UserId = "u2", UserLogin = "bob", DisplayName = "Bob", TeamNames = ["Penguin Squad"] },
                new() { UserId = "u3", UserLogin = "charlie", DisplayName = "Charlie", TeamNames = ["Penguin Squad"] }
            });

            twitchService.AreStreamsOnline(Arg.Any<List<string>>()).Returns(new List<PenguinTwitchBot.Bot.TwitchServices.TwitchModels.OnlineStream>
            {
                new() { UserId = "u1", UserLogin = "alice", UserName = "Alice", DisplayName = "Alice", Game = "Balatro" },
                new() { UserId = "u2", UserLogin = "bob", UserName = "Bob", DisplayName = "Bob", Game = "Minecraft" }
            });

            var raidTracker = new RaidTracker(
                Substitute.For<ILogger<RaidTracker>>(),
                scopeFactory,
                twitchService,
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>(),
                Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());

            var result = await raidTracker.GetDisplayHistoryAsync(includeLiveTeamMembers: true);

            // Should contain u1 (history + live team), u2 (live team only), u9 (history non-team) — NOT u3 (offline team only)
            Assert.Equal(3, result.Count);
            Assert.DoesNotContain(result, x => x.UserId == "u3");

            var alice = Assert.Single(result, x => x.UserId == "u1");
            Assert.True(alice.IsHistoryEntry);
            Assert.True(alice.IsStreamTeamMember);
            Assert.True(alice.IsOnline);
            Assert.Equal("Balatro", alice.LastGame);
            Assert.Equal(3, alice.TotalIncomingRaids);
            Assert.Equal(["Penguin Squad"], alice.StreamTeamNames);

            var bob = Assert.Single(result, x => x.UserId == "u2");
            Assert.False(bob.IsHistoryEntry);
            Assert.True(bob.IsStreamTeamMember);
            Assert.True(bob.IsOnline);
            Assert.Equal("bob", bob.Name);
            Assert.Equal("Bob", bob.DisplayName);
            Assert.Equal("Minecraft", bob.LastGame);
            Assert.Equal(DateTime.MinValue, bob.LastIncomingRaid);
            Assert.Equal(DateTime.MinValue, bob.LastOutgoingRaid);

            var nonTeam = Assert.Single(result, x => x.UserId == "u9");
            Assert.True(nonTeam.IsHistoryEntry);
            Assert.False(nonTeam.IsStreamTeamMember);
        }

        [Fact]
        public async Task GetDisplayHistoryAsync_WhenNotInAnyTeam_DoesNotCheckOnlineStreams()
        {
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var twitchService = Substitute.For<ITwitchService>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            dbContext.RaidHistory.GetAllAsync().Returns(new List<RaidHistoryEntry>
            {
                new() { Id = Guid.NewGuid(), UserId = "u1", Name = "alice", DisplayName = "Alice", IsOnline = true }
            });
            twitchService.GetStreamTeamMembersAsync().Returns([]);

            var raidTracker = new RaidTracker(
                Substitute.For<ILogger<RaidTracker>>(),
                scopeFactory,
                twitchService,
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>(),
                Substitute.For<PenguinTwitchBot.Services.IRaidRewardService>());

            var result = await raidTracker.GetDisplayHistoryAsync(includeLiveTeamMembers: true);

            var item = Assert.Single(result);
            Assert.False(item.IsStreamTeamMember);
            await twitchService.DidNotReceive().AreStreamsOnline(Arg.Any<List<string>>());
        }
    }
}
