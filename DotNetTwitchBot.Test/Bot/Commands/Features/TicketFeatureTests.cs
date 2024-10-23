using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Commands.Features
{
    public class TicketFeatureTests
    {
        [Fact]
        public async Task GiveTicketsToViewer_ShouldSucceed()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            viewerFeature.GetViewerByUserName("test").Returns(new Viewer());

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<ViewerTicket> { new ViewerTicket() }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            var result = await ticketFeature.GiveTicketsToViewer("test", 5);

            //Assert
            Assert.Equal(5, result);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GiveTicketsToViewer_ShouldAddTickets()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();

            var viewerFeature = Substitute.For<IViewerFeature>();
            viewerFeature.GetViewerByUserName("test").Returns(new Viewer());

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testUser = new ViewerTicket { Points = 10 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            var result = await ticketFeature.GiveTicketsToViewer("test", 5);

            //Assert
            Assert.Equal(15, result);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GiveTicketsWithBonusToViewers_ShouldAddTickets()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testUser = new ViewerTicket { Points = 0 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var testViewer = new Viewer();
            viewerFeature.GetViewerByUserName(Arg.Any<string>()).Returns(testViewer);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            await ticketFeature.GiveTicketsWithBonusToViewers(new List<string> { "test" }, 5, 5);

            //Assert
            Assert.Equal(5, testUser.Points);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GiveTicketsWithBonusToViewers_ShouldAddTickets_WithBonus()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testUser = new ViewerTicket { Points = 0 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var testViewer = new Viewer { isSub = true };
            viewerFeature.GetViewerByUserName(Arg.Any<string>()).Returns(testViewer);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            await ticketFeature.GiveTicketsWithBonusToViewers(new List<string> { "test" }, 5, 5);

            //Assert
            Assert.Equal(10, testUser.Points);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GiveTicketsToActiveUsers_NotActiveNotSub_NoTickets()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testUser = new ViewerTicket { Points = 0 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var testViewer = new Viewer { isSub = false };
            viewerFeature.GetViewerByUserName(Arg.Any<string>()).Returns(testViewer);
            viewerFeature.GetActiveViewers().Returns(new List<string>());
            viewerFeature.GetCurrentViewers().Returns(new List<string> { "test" });
            viewerFeature.IsSubscriber("test").Returns(false);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            await ticketFeature.GiveTicketsToActiveUsers(5);

            //Assert
            Assert.Equal(0, testUser.Points);
        }

        [Fact]
        public async Task GiveTicketsToActiveUsers_NotActiveIsSub_GetTicketsTickets()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testUser = new ViewerTicket { Points = 0 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var testViewer = new Viewer { isSub = false };
            viewerFeature.GetViewerByUserName(Arg.Any<string>()).Returns(testViewer);
            viewerFeature.GetActiveViewers().Returns(new List<string>());
            viewerFeature.GetCurrentViewers().Returns(new List<string> { "test" });
            viewerFeature.IsSubscriber("test").Returns(true);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            await ticketFeature.GiveTicketsToActiveUsers(5);

            //Assert
            Assert.Equal(5, testUser.Points);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GiveTicketsToActiveUsers_IsActiveNotSub_GetTicketsTickets()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testUser = new ViewerTicket { Points = 0 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var testViewer = new Viewer { isSub = false };
            viewerFeature.GetViewerByUserName(Arg.Any<string>()).Returns(testViewer);
            viewerFeature.GetActiveViewers().Returns(new List<string> { "test" });
            viewerFeature.GetCurrentViewers().Returns(new List<string> { "test" });
            viewerFeature.IsSubscriber("test").Returns(false);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            await ticketFeature.GiveTicketsToActiveUsers(5);

            //Assert
            Assert.Equal(5, testUser.Points);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GiveTicketsToActiveAndSubsOnlineWithBonus_NotActiveNotSub_NoTickets()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testUser = new ViewerTicket { Points = 0 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var testViewer = new Viewer { isSub = false };
            viewerFeature.GetViewerByUserName(Arg.Any<string>()).Returns(testViewer);
            viewerFeature.GetActiveViewers().Returns(new List<string>());
            viewerFeature.GetCurrentViewers().Returns(new List<string> { "test" });
            viewerFeature.IsSubscriber("test").Returns(false);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            await ticketFeature.GiveTicketsToActiveAndSubsOnlineWithBonus(5, 5);

            //Assert
            Assert.Equal(0, testUser.Points);
        }

        [Fact]
        public async Task GiveTicketsToActiveAndSubsOnlineWithBonus_NotActiveIsSub_GetTicketsTickets()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testUser = new ViewerTicket { Points = 0 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var testViewer = new Viewer { isSub = true };
            viewerFeature.GetViewerByUserName(Arg.Any<string>()).Returns(testViewer);
            viewerFeature.GetActiveViewers().Returns(new List<string>());
            viewerFeature.GetCurrentViewers().Returns(new List<string> { "test" });
            viewerFeature.IsSubscriber("test").Returns(true);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            await ticketFeature.GiveTicketsToActiveAndSubsOnlineWithBonus(5, 5);

            //Assert
            Assert.Equal(10, testUser.Points);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GiveTicketsToActiveAndSubsOnlineWithBonus_IsActiveNotSub_GetTicketsTickets()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testUser = new ViewerTicket { Points = 0 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var testViewer = new Viewer { isSub = false };
            viewerFeature.GetViewerByUserName(Arg.Any<string>()).Returns(testViewer);
            viewerFeature.GetActiveViewers().Returns(new List<string> { "test" });
            viewerFeature.GetCurrentViewers().Returns(new List<string> { "test" });
            viewerFeature.IsSubscriber("test").Returns(false);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            await ticketFeature.GiveTicketsToActiveAndSubsOnlineWithBonus(5, 5);

            //Assert
            Assert.Equal(5, testUser.Points);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GetViewerTickets_UserDoesntExist_ShouldReturnZero()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<ViewerTicket> { }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            var result = await ticketFeature.GetViewerTickets("test");

            //Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task GetViewerTickets_UserDoesExist_ShouldReturnValue()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var viewerTicket = new ViewerTicket { Points = 5 };
            var queryable = new List<ViewerTicket> { viewerTicket }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            var result = await ticketFeature.GetViewerTickets("test");

            //Assert
            Assert.Equal(5, result);
        }

        [Fact]
        public async Task GetViewerTicketsWithRank_UserDoesntExist_ShouldReturnZero()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<ViewerTicketWithRanks> { }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTicketsWithRank.Find(x => true).ReturnsForAnyArgs(queryable);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            var result = await ticketFeature.GetViewerTicketsWithRank("test");

            //Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetViewerTicketsWithRank_UserDoesExist_ShouldReturnValue()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var viewerTicket = new ViewerTicketWithRanks { Points = 5 };
            var queryable = new List<ViewerTicketWithRanks> { viewerTicket }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTicketsWithRank.Find(x => true).ReturnsForAnyArgs(queryable);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            var result = await ticketFeature.GetViewerTicketsWithRank("test");

            //Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.Points);
        }

        [Fact]
        public async Task RemoveTicketsFromViewer_ShouldRemoveTickets()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            viewerFeature.GetViewerByUserName("test").Returns(new Viewer());

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testUser = new ViewerTicket { Points = 10 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);

            //Act
            var result = await ticketFeature.RemoveTicketsFromViewer("test", 5);

            //Assert
            Assert.Equal(5, testUser.Points);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task OnCommand_Tickets_ShouldSayMessage()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var viewerTicket = new ViewerTicketWithRanks { Points = 5 };
            var queryable = new List<ViewerTicketWithRanks> { viewerTicket }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTicketsWithRank.Find(x => true).ReturnsForAnyArgs(queryable);

            commandHandler.GetCommandDefaultName("tickets").Returns("tickets");

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);
            var eventArgs = new DotNetTwitchBot.Bot.Events.Chat.CommandEventArgs
            {
                Command = "tickets",
                Name = "Test",
                DisplayName = "Test"
            };
            //Act
            await ticketFeature.OnCommand(null, eventArgs);

            //Assert
            await serviceBackbone.Received(1).SendChatMessage(Arg.Is<string>(x => x.Contains("You are currently ranked")));
        }

        [Fact]
        public async Task OnCommand_Tickets_ShouldSaNoTicketsMessage()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var viewerTicket = new ViewerTicketWithRanks { Points = 5 };
            var queryable = new List<ViewerTicketWithRanks> { }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTicketsWithRank.Find(x => true).ReturnsForAnyArgs(queryable);

            commandHandler.GetCommandDefaultName("tickets").Returns("tickets");

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);
            var eventArgs = new DotNetTwitchBot.Bot.Events.Chat.CommandEventArgs
            {
                Command = "tickets",
                Name = "Test",
                DisplayName = "Test"
            };
            //Act
            await ticketFeature.OnCommand(null, eventArgs);

            //Assert
            await serviceBackbone.Received(1).SendChatMessage("Test", Arg.Is<string>(x => x.Contains("hang around and you will")));
        }

        [Fact]
        public async Task OnCommand_ShouldSucceed()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            viewerFeature.GetViewerByUserName("Test").Returns(new Viewer());

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var testViewerTickets = new ViewerTicket();
            var queryable = new List<ViewerTicket> { testViewerTickets }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            commandHandler.GetCommandDefaultName("givetickets").Returns("givetickets");

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, viewerFeature, commandHandler);
            var eventArgs = new DotNetTwitchBot.Bot.Events.Chat.CommandEventArgs
            {
                Command = "givetickets",
                Name = "Test",
                DisplayName = "Test",
                TargetUser = "Test",
                Args = new List<string> { "", "10" }
            };
            //Act
            await ticketFeature.OnCommand(null, eventArgs);

            //Assert
            Assert.Equal(10, testViewerTickets.Points);
        }

        [Fact]
        public async Task ResetAllPoints_ShouldSucceed()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, Substitute.For<IViewerFeature>(), commandHandler);

            //Act
            await ticketFeature.ResetAllPoints();

            //Assert
            await dbContext.ViewerTickets.Received(1).ExecuteDeleteAllAsync();
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task OnCommand_ResetAllPoints_ShouldSucceed()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, scopeFactory, Substitute.For<IViewerFeature>(), commandHandler);

            commandHandler.GetCommandDefaultName("resettickets").Returns("resettickets");

            var eventArgs = new DotNetTwitchBot.Bot.Events.Chat.CommandEventArgs
            {
                Command = "resettickets",
                Name = "Test",
                DisplayName = "Test",
                TargetUser = "Test",
                Args = new List<string> { "", "10" }
            };
            //Act
            await ticketFeature.OnCommand(null, eventArgs);

            //Assert
            await dbContext.ViewerTickets.Received(1).ExecuteDeleteAllAsync();
            await dbContext.Received(1).SaveChangesAsync();
        }
    }
}
