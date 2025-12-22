using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace DotNetTwitchBot.Test.Bot.Commands.Misc
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
            var mediatorSubstitute = Substitute.For<IMediator>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { new RaidHistoryEntry() }.AsQueryable();
            dbContext.RaidHistory.GetAllAsync().Returns(queryable);

            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, Substitute.For<ITwitchService>(), Substitute.For<IServiceBackbone>(), mediatorSubstitute, Substitute.For<ICommandHandler>());
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
            var mediatorSubstitute = Substitute.For<IMediator>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { new RaidHistoryEntry() }.AsQueryable();
            dbContext.RaidHistory.GetAllAsync().Returns(queryable);

            var twitchService = Substitute.For<ITwitchService>();
            twitchService.GetUserByName("").ReturnsNull();

            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, Substitute.For<IServiceBackbone>(), mediatorSubstitute, Substitute.For<ICommandHandler>());
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
            var mediatorSubstitute = Substitute.For<IMediator>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { new RaidHistoryEntry() }.AsQueryable();
            dbContext.RaidHistory.GetAllAsync().Returns(queryable);

            var twitchService = Substitute.For<ITwitchService>();
            twitchService.GetUserByName("").Returns(new User());
            twitchService.IsStreamOnline(Arg.Any<string>()).Returns(false);

            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, Substitute.For<IServiceBackbone>(), mediatorSubstitute, Substitute.For<ICommandHandler>());
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
            var mediatorSubstitute = Substitute.For<IMediator>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { new RaidHistoryEntry() }.BuildMockDbSet().AsQueryable();
            dbContext.RaidHistory.Find(x => true).ReturnsForAnyArgs(queryable);

            var twitchService = Substitute.For<ITwitchService>();
            twitchService.GetUserByName("").Returns(new User());
            twitchService.IsStreamOnline(Arg.Any<string>()).Returns(true);

            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, serviceBackbone, mediatorSubstitute, Substitute.For<ICommandHandler>());
            //Act
            await raidTracker.Raid("");

            //Assert
            dbContext.RaidHistory.Received(1).Update(Arg.Any<RaidHistoryEntry>());
            await dbContext.Received(1).SaveChangesAsync();
            await serviceBackbone.Received(1).SendChatMessage(Arg.Any<string>());
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
            var mediatorSubstitute = Substitute.For<IMediator>();
            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { }.BuildMockDbSet().AsQueryable();
            dbContext.RaidHistory.Find(x => true).ReturnsForAnyArgs(queryable);

            twitchService.GetUserId(Arg.Any<string>()).Returns("");
            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, serviceBackbone, mediatorSubstitute, Substitute.For<ICommandHandler>());

            //Act
            await raidTracker.OnIncomingRaid(new DotNetTwitchBot.Bot.Events.RaidEventArgs());

            //Assert
            dbContext.RaidHistory.Received(1).Update(Arg.Any<RaidHistoryEntry>());
            await dbContext.Received(1).SaveChangesAsync();
            await serviceBackbone.Received(1).SendChatMessage(Arg.Any<string>());
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
            var mediatorSubstitute = Substitute.For<IMediator>();
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
            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, serviceBackbone, mediatorSubstitute, Substitute.For<ICommandHandler>());

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
            var mediatorSubstitute = Substitute.For<IMediator>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<RaidHistoryEntry> { new RaidHistoryEntry() }.BuildMockDbSet().AsQueryable();
            dbContext.RaidHistory.Find(x => true).ReturnsForAnyArgs(queryable);

            var twitchService = Substitute.For<ITwitchService>();
            twitchService.GetUserByName("").Returns(new User());
            twitchService.IsStreamOnline(Arg.Any<string>()).Returns(true);

            commandHandler.GetCommandDefaultName("raid").Returns("raid");

            var raidTracker = new RaidTracker(Substitute.For<ILogger<RaidTracker>>(), scopeFactory, twitchService, serviceBackbone, mediatorSubstitute, commandHandler);
            //Act
            await raidTracker.OnCommand(null, new DotNetTwitchBot.Bot.Events.Chat.CommandEventArgs
            {
                TargetUser = "",
                Command = "raid"
            });

            //Assert
            dbContext.RaidHistory.Received(1).Update(Arg.Any<RaidHistoryEntry>());
            await dbContext.Received(1).SaveChangesAsync();
            await serviceBackbone.Received(1).SendChatMessage(Arg.Any<string>());
        }
    }
}
