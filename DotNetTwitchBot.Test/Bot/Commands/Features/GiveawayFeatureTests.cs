using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.Models.Giveaway;
using DotNetTwitchBot.Bot.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Commands.Features
{
    public class GiveawayFeatureTests
    {
        private readonly ILogger<GiveawayFeature> logger;
        private readonly ICommandHandler commandHandler;
        private readonly IServiceScope scope;
        private readonly IServiceBackbone serviceBackbone;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ITicketsFeature ticketsFeature;
        private readonly IViewerFeature viewerFeature;
        private readonly IHubContext<GiveawayHub> hubContext;
        private readonly IUnitOfWork dbContext;
        private readonly IServiceProvider serviceProvider;
        private readonly Setting testPrize;
        private readonly DbSet<Setting> prizeQueryable;
        private readonly DbSet<Setting> emptyPrizeQueryable;
        private readonly GiveawayEntry testGiveawayEntriesA;
        private readonly GiveawayEntry testGiveawayEntriesB;
        private readonly DbSet<GiveawayEntry> testGiveawayEntriesQueryable;
        private readonly DbSet<GiveawayEntry> emptyTestGiveawayEntriesQueryable;
        private readonly GiveawayWinner testPastWinners;
        private readonly DbSet<GiveawayWinner> pastWinnersQueryable;
        private readonly GiveawayFeature giveawayFeature;

        public GiveawayFeatureTests()
        {
            scopeFactory = Substitute.For<IServiceScopeFactory>();
            dbContext = Substitute.For<IUnitOfWork>();
            serviceProvider = Substitute.For<IServiceProvider>();
            scope = Substitute.For<IServiceScope>();
            serviceBackbone = Substitute.For<IServiceBackbone>();
            logger = Substitute.For<ILogger<GiveawayFeature>>();
            commandHandler = Substitute.For<ICommandHandler>();
            ticketsFeature = Substitute.For<ITicketsFeature>();
            viewerFeature = Substitute.For<IViewerFeature>();
            hubContext = Substitute.For<IHubContext<GiveawayHub>>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            testPrize = new Setting { Name = "GiveawayPrize", StringSetting = "Test Prize", Id = 1 };
            prizeQueryable = new List<Setting> { testPrize }.AsQueryable().BuildMockDbSet();
            emptyPrizeQueryable = new List<Setting> { }.AsQueryable().BuildMockDbSet();

            testGiveawayEntriesA = new GiveawayEntry { Username = "UserA", Tickets = 10 };
            testGiveawayEntriesB = new GiveawayEntry { Username = "UserB", Tickets = 100 };
            testGiveawayEntriesQueryable = new List<GiveawayEntry> { testGiveawayEntriesA, testGiveawayEntriesB }.AsQueryable().BuildMockDbSet();
            emptyTestGiveawayEntriesQueryable = new List<GiveawayEntry> { }.AsQueryable().BuildMockDbSet();

            testPastWinners = new GiveawayWinner { Username = "WINNER", Prize = "Test Prize" };
            pastWinnersQueryable = new List<GiveawayWinner> { testPastWinners }.AsQueryable().BuildMockDbSet();

            giveawayFeature = new GiveawayFeature(logger, serviceBackbone, ticketsFeature, viewerFeature, hubContext, scopeFactory, commandHandler);

        }


        [Fact]
        public async Task GetPrize_ShouldReturnPrize()
        {
            // Arrange
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(prizeQueryable);

            // Act
            var result = await giveawayFeature.GetPrize();

            // Assert
            Assert.Equal("Test Prize", result);

        }

        [Fact]
        public async Task GetPrize_ShouldReturnNoPrize()
        {
            // Arrange
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(emptyPrizeQueryable);

            // Act
            var result = await giveawayFeature.GetPrize();

            // Assert
            Assert.Equal("No Prize", result);

        }

        [Fact]
        public async Task SetPrize_ShouldUpdate()
        {
            // Arrange
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(prizeQueryable);

            // Act
            await giveawayFeature.SetPrize("New Prize Name");

            // Assert
            Assert.Equal("New Prize Name", testPrize.StringSetting);
            dbContext.Settings.Received(1).Update(testPrize);
            await dbContext.Received(1).SaveChangesAsync();

        }

        [Fact]
        public async Task SetPrize_ShouldAdd()
        {
            // Arrange
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(emptyPrizeQueryable);

            // Act
            await giveawayFeature.SetPrize("New Prize Name");

            // Assert
            await dbContext.Settings.Received(1).AddAsync(Arg.Any<Setting>());
            await dbContext.Received(1).SaveChangesAsync();

        }


        [Fact]
        public async Task Close_ShouldCloseAndCompileTickets()
        {
            // Arrange
            dbContext.GiveawayEntries.GetAllAsync().Returns(testGiveawayEntriesQueryable);

            // Act
            await giveawayFeature.Close();

            // Assert
            Assert.NotEmpty(giveawayFeature.ClosedTickets);
            Assert.Equal(110, giveawayFeature.ClosedTickets.Count);
            Assert.Equal(10, giveawayFeature.ClosedTickets.Where(x => x.Equals("UserA")).Count());
            Assert.Equal(100, giveawayFeature.ClosedTickets.Where(x => x.Equals("UserB")).Count());

        }


        [Fact]
        public async Task Reset_ShouldReset()
        {
            // Arrange
            dbContext.GiveawayEntries.GetAllAsync().Returns(testGiveawayEntriesQueryable);
            await giveawayFeature.Close();

            // Act
            await giveawayFeature.Reset();

            // Assert
            Assert.Empty(giveawayFeature.ClosedTickets);
            await dbContext.GiveawayEntries.Received(1).ExecuteDeleteAsync();
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task Draw_ShouldCloseAndDrawWinner()
        {
            // Arrange
            dbContext.GiveawayEntries.GetAllAsync().Returns(testGiveawayEntriesQueryable);
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(prizeQueryable);
            viewerFeature.IsFollower(Arg.Any<string>()).Returns(true);
            var viewer = new Viewer { DisplayName = "Displayed Name", Title = "" };
            viewerFeature.GetViewer(Arg.Any<string>()).Returns(viewer);

            // Act
            await giveawayFeature.Draw();

            // Assert
            Assert.NotEmpty(giveawayFeature.ClosedTickets);
            await dbContext.GiveawayWinners.Received(1).AddAsync(Arg.Any<GiveawayWinner>());
            await dbContext.Received(1).SaveChangesAsync();
            await serviceBackbone.Received(1).SendChatMessage(Arg.Is<string>(x => x.Contains("won the")));

        }


        [Fact]
        public async Task PastWinners_ShouldRetrievePast()
        {
            // Arrange
            dbContext.GiveawayWinners.GetAllAsync().Returns(pastWinnersQueryable);

            // Act
            var result = await giveawayFeature.PastWinners();

            // Assert
            Assert.NotEmpty(result);
            Assert.Single(result);
        }

        [Theory]
        [InlineData("10")]
        [InlineData("all")]
        [InlineData("max")]
        [InlineData("ALL")]
        [InlineData("MAX")]
        public async Task OnCommand_Enter_ShouldEnter(string enterAmount)
        {
            // Arrange
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(testGiveawayEntriesQueryable);
            var viewer = new Viewer { DisplayName = "Displayed Name", Title = "" };
            viewerFeature.GetViewer(Arg.Any<string>()).Returns(viewer);

            ticketsFeature.GetViewerTickets(Arg.Any<string>()).Returns(10);
            viewerFeature.GetDisplayName(Arg.Any<string>()).Returns("");
            ticketsFeature.RemoveTicketsFromViewer("user", 10).Returns(true);

            var commandEvent = new CommandEventArgs { Command = "enter", Args = new List<string> { enterAmount }, DisplayName = "user", Name = "user" };
            var commandBaseProps = new BaseCommandProperties { CommandName = "enter" };
            var commandBaseSvc = Substitute.For<IBaseCommandService>();
            var command = new Command(commandBaseProps, commandBaseSvc);
            commandHandler.GetCommand("enter").Returns(command);

            // Act
            await giveawayFeature.OnCommand(new object(), commandEvent);

            // Assert
            dbContext.GiveawayEntries.Received(1).Update(Arg.Any<GiveawayEntry>());
            await dbContext.Received(1).SaveChangesAsync();
            dbContext.GiveawayEntries.Received(1).Update(Arg.Is<GiveawayEntry>(x => x.Tickets == 20));
        }

        [Theory]
        [InlineData("foobar")]
        [InlineData("20")]
        [InlineData("-10")]
        [InlineData("")]
        public async Task OnCommand_Enter_ShouldThrow(string enterAmount)
        {
            // Arrange
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(testGiveawayEntriesQueryable);
            var viewer = new Viewer { DisplayName = "Displayed Name", Title = "" };
            viewerFeature.GetViewer(Arg.Any<string>()).Returns(viewer);

            ticketsFeature.GetViewerTickets(Arg.Any<string>()).Returns(10);
            viewerFeature.GetDisplayName(Arg.Any<string>()).Returns("");
            ticketsFeature.RemoveTicketsFromViewer("user", 10).Returns(true);

            var commandEvent = new CommandEventArgs { Command = "enter", Args = new List<string> { enterAmount }, DisplayName = "user", Name = "user" };
            var commandBaseProps = new BaseCommandProperties { CommandName = "enter" };
            var commandBaseSvc = Substitute.For<IBaseCommandService>();
            var command = new Command(commandBaseProps, commandBaseSvc);
            commandHandler.GetCommand("enter").Returns(command);

            // Act

            // Assert
            await Assert.ThrowsAsync<SkipCooldownException>(async () => await giveawayFeature.OnCommand(new object(), commandEvent));
        }

        [Theory]
        [InlineData("1000000")]
        public async Task OnCommand_Enter_ShouldThrowWhenToMuch(string enterAmount)
        {
            // Arrange
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(testGiveawayEntriesQueryable);
            var viewer = new Viewer { DisplayName = "Displayed Name", Title = "" };
            viewerFeature.GetViewer(Arg.Any<string>()).Returns(viewer);

            ticketsFeature.GetViewerTickets(Arg.Any<string>()).Returns(1000000);
            viewerFeature.GetDisplayName(Arg.Any<string>()).Returns("");
            ticketsFeature.RemoveTicketsFromViewer("user", 10).Returns(true);

            var commandEvent = new CommandEventArgs { Command = "enter", Args = new List<string> { enterAmount }, DisplayName = "user", Name = "user" };
            var commandBaseProps = new BaseCommandProperties { CommandName = "enter" };
            var commandBaseSvc = Substitute.For<IBaseCommandService>();
            var command = new Command(commandBaseProps, commandBaseSvc);
            commandHandler.GetCommand("enter").Returns(command);

            // Act

            // Assert
            await Assert.ThrowsAsync<SkipCooldownException>(async () => await giveawayFeature.OnCommand(new object(), commandEvent));
        }


        [Fact]
        public async Task OnCommand_Draw()
        {
            // Arrange
            dbContext.GiveawayEntries.GetAllAsync().Returns(testGiveawayEntriesQueryable);
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(prizeQueryable);
            viewerFeature.IsFollower(Arg.Any<string>()).Returns(true);
            var viewer = new Viewer { DisplayName = "Displayed Name", Title = "" };
            viewerFeature.GetViewer(Arg.Any<string>()).Returns(viewer);



            var commandEvent = new CommandEventArgs { Command = "draw" };
            var commandBaseProps = new BaseCommandProperties { CommandName = "draw" };
            var commandBaseSvc = Substitute.For<IBaseCommandService>();
            var command = new Command(commandBaseProps, commandBaseSvc);
            commandHandler.GetCommand("draw").Returns(command);

            // Act
            await giveawayFeature.OnCommand(new object(), commandEvent);

            // Assert
            Assert.NotEmpty(giveawayFeature.ClosedTickets);
            await dbContext.GiveawayWinners.Received(1).AddAsync(Arg.Any<GiveawayWinner>());
            await dbContext.Received(1).SaveChangesAsync();
            await serviceBackbone.Received(1).SendChatMessage(Arg.Is<string>(x => x.Contains("won the")));
        }

        [Fact]
        public async Task OnCommand_Close()
        {
            // Arrange
            dbContext.GiveawayEntries.GetAllAsync().Returns(testGiveawayEntriesQueryable);
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(prizeQueryable);
            viewerFeature.IsFollower(Arg.Any<string>()).Returns(true);
            var viewer = new Viewer { DisplayName = "Displayed Name", Title = "" };
            viewerFeature.GetViewer(Arg.Any<string>()).Returns(viewer);



            var commandEvent = new CommandEventArgs { Command = "close" };
            var commandBaseProps = new BaseCommandProperties { CommandName = "close" };
            var commandBaseSvc = Substitute.For<IBaseCommandService>();
            var command = new Command(commandBaseProps, commandBaseSvc);
            commandHandler.GetCommand("close").Returns(command);

            // Act
            await giveawayFeature.OnCommand(new object(), commandEvent);

            // Assert
            Assert.NotEmpty(giveawayFeature.ClosedTickets);
        }

        [Fact]
        public async Task OnCommand_ResetDraw()
        {
            // Arrange
            dbContext.GiveawayEntries.GetAllAsync().Returns(testGiveawayEntriesQueryable);
            await giveawayFeature.Close();

            var commandEvent = new CommandEventArgs { Command = "resetdraw" };
            var commandBaseProps = new BaseCommandProperties { CommandName = "resetdraw" };
            var commandBaseSvc = Substitute.For<IBaseCommandService>();
            var command = new Command(commandBaseProps, commandBaseSvc);
            commandHandler.GetCommand("resetdraw").Returns(command);

            // Act
            await giveawayFeature.OnCommand(new object(), commandEvent);

            // Assert
            Assert.Empty(giveawayFeature.ClosedTickets);
        }

        [Fact]
        public async Task OnCommand_SetPrize()
        {
            // Arrange
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(prizeQueryable);
            var commandEvent = new CommandEventArgs { Command = "setprize", Arg = "New Prize Name 2" };
            var commandBaseProps = new BaseCommandProperties { CommandName = "setprize" };
            var commandBaseSvc = Substitute.For<IBaseCommandService>();
            var command = new Command(commandBaseProps, commandBaseSvc);
            commandHandler.GetCommand("setprize").Returns(command);

            // Act
            await giveawayFeature.OnCommand(new object(), commandEvent);

            // Assert
            Assert.Equal("New Prize Name 2", testPrize.StringSetting);
            dbContext.Settings.Received(1).Update(testPrize);
            await dbContext.Received(1).SaveChangesAsync();

        }

    }
}
