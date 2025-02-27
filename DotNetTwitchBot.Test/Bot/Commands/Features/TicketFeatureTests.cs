using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
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
        public async Task GiveTicketsToActiveUsers_NotActiveNotSub_NoTickets()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var pointsSystem = Substitute.For<IPointsSystem>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            var testUser = new ViewerTicket { Points = 0 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var testViewer = new Viewer { isSub = false };
            viewerFeature.GetViewerByUserName(Arg.Any<string>()).Returns(testViewer);
            viewerFeature.GetActiveViewers().Returns(new List<string>());
            viewerFeature.GetCurrentViewers().Returns(new List<string> { "test" });
            viewerFeature.IsSubscriber("test").Returns(false);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, pointsSystem, viewerFeature, commandHandler);

            //Act
            await ticketFeature.GiveTicketsToActiveUsers(5);

            //Assert
            Assert.Equal(0, testUser.Points);
        }

        [Fact]
        public async Task GiveTicketsToActiveAndSubsOnlineWithBonus_NotActiveNotSub_NoTickets()
        {
            //Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var pointsSystem = Substitute.For<IPointsSystem>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            var testUser = new ViewerTicket { Points = 0 };
            var queryable = new List<ViewerTicket> { testUser }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerTickets.Find(x => true).ReturnsForAnyArgs(queryable);

            var testViewer = new Viewer { isSub = false };
            viewerFeature.GetViewerByUserName(Arg.Any<string>()).Returns(testViewer);
            viewerFeature.GetActiveViewers().Returns(new List<string>());
            viewerFeature.GetCurrentViewers().Returns(new List<string> { "test" });
            viewerFeature.IsSubscriber("test").Returns(false);

            var ticketFeature = new TicketsFeature(Substitute.For<ILogger<TicketsFeature>>(), serviceBackbone, pointsSystem, viewerFeature, commandHandler);

            //Act
            await ticketFeature.GiveTicketsToActiveAndSubsOnlineWithBonus(5, 5);

            //Assert
            Assert.Equal(0, testUser.Points);
        }



    }
}
