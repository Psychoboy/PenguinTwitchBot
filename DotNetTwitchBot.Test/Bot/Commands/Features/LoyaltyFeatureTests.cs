using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.CustomMiddleware;
using DotNetTwitchBot.Repository;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Commands.Features
{
    public class LoyaltyFeatureTests
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IUnitOfWork dbContext;
        private readonly IServiceProvider serviceProvider;
        private readonly IServiceScope scope;
        private readonly IServiceBackbone serviceBackbone;
        private readonly ILogger<LoyaltyFeature> logger;
        private readonly ICommandHandler commandHandler;
        private readonly IViewerFeature viewerFeature;
        private readonly ITicketsFeature ticketFeature;
        private readonly LoyaltyFeature loyaltyFeature;
        private readonly IPointsSystem pointsSystem;

        public LoyaltyFeatureTests()
        {
            scopeFactory = Substitute.For<IServiceScopeFactory>();
            dbContext = Substitute.For<IUnitOfWork>();
            serviceProvider = Substitute.For<IServiceProvider>();
            scope = Substitute.For<IServiceScope>();
            serviceBackbone = Substitute.For<IServiceBackbone>();
            logger = Substitute.For<ILogger<LoyaltyFeature>>();
            commandHandler = Substitute.For<ICommandHandler>();
            viewerFeature = Substitute.For<IViewerFeature>();
            ticketFeature = Substitute.For<ITicketsFeature>();
            pointsSystem = Substitute.For<IPointsSystem>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            loyaltyFeature = new LoyaltyFeature(logger, viewerFeature, scopeFactory, serviceBackbone, ticketFeature, commandHandler, pointsSystem);
        }


        [Fact]
        public async Task OnCheer_Anonymous()
        {
            // Arrange
            var cheerEventArgs = new CheerEventArgs { IsAnonymous = true, Amount = 100 };
            await loyaltyFeature.StartAsync(default);

            // Act
            serviceBackbone.CheerEvent += Raise.Event<AsyncEventHandler<CheerEventArgs>>(this, cheerEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("Someone just cheered 100 bits! sptvHype");
        }

        [Fact]
        public async Task OnCheer_ShouldGiveTickets()
        {
            // Arrange
            var cheerEventArgs = new CheerEventArgs { DisplayName = "TestName", Name = "testname", IsAnonymous = false, Amount = 100, UserId = "123" };
            var queryable = new List<Setting> { }.AsQueryable().BuildMockDbSet();
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(queryable);
            await loyaltyFeature.StartAsync(default);
            // Act
            serviceBackbone.CheerEvent += Raise.Event<AsyncEventHandler<CheerEventArgs>>(this, cheerEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("TestName just cheered 100 bits! sptvHype");
            await ticketFeature.Received(1).GiveTicketsToViewerByUserId("123", 10);
        }


        [Fact]
        public async Task OnSubscriptionGift_ShouldGiveTickets_AndSayTotalGiven()
        {
            // Arrange
            var subscriptionEvent = new SubscriptionGiftEventArgs { Name = "testname", DisplayName = "TestName", GiftAmount = 5, TotalGifted = 10, UserId = "123" };
            var queryable = new List<Setting> { }.AsQueryable().BuildMockDbSet();
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(queryable);
            await loyaltyFeature.StartAsync(default);

            // Act
            serviceBackbone.SubscriptionGiftEvent += Raise.Event<AsyncEventHandler<SubscriptionGiftEventArgs>>(this, subscriptionEvent);

            // Assert
            await ticketFeature.Received(1).GiveTicketsToViewerByUserId("123", 250);
            await serviceBackbone.Received(1).SendChatMessage("TestName gifted 5 subscriptions to the channel! sptvHype sptvHype sptvHype They have gifted a total of 10 subs to the channel!");

        }

        [Fact]
        public async Task OnChatMessage_ShouldUpdateMessageCount()
        {
            // Arrange
            var testViewer = new ViewerMessageCount();
            var queryable = new List<ViewerMessageCount> { testViewer }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerMessageCounts.Find(x => true).ReturnsForAnyArgs(queryable);
            var chatMessageEvent = new ChatMessageEventArgs { };
            serviceBackbone.IsOnline = true;
            serviceBackbone.IsKnownBotOrCurrentStreamer(Arg.Any<string>()).Returns(false);

            // Act
            await loyaltyFeature.OnChatMessage(chatMessageEvent);

            // Assert
            dbContext.ViewerMessageCounts.Received(1).Update(testViewer);
            await dbContext.Received(1).SaveChangesAsync();
            Assert.Equal(1, testViewer.MessageCount);
        }

        [Fact]
        public async Task OnChatMessage_ShouldCreateMessageCount()
        {
            // Arrange
            var queryable = new List<ViewerMessageCount> { }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerMessageCounts.Find(x => true).ReturnsForAnyArgs(queryable);
            var chatMessageEvent = new ChatMessageEventArgs { };
            serviceBackbone.IsOnline = true;
            serviceBackbone.IsKnownBotOrCurrentStreamer(Arg.Any<string>()).Returns(false);

            // Act
            await loyaltyFeature.OnChatMessage(chatMessageEvent);

            // Assert
            dbContext.ViewerMessageCounts.Received(1).Update(Arg.Any<ViewerMessageCount>());
            await dbContext.Received(1).SaveChangesAsync();
        }

        //[Fact]
        //public async Task OnCommand_CheckPasties_NoPasties()
        //{
        //    // Arrange
        //    var viewerPoint = new ViewerPoint();
        //    var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
        //    dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);
        //    var commandEventArgs = new CommandEventArgs { Args = new List<string> { "TestTarget" }, TargetUser = "TestTarget", DisplayName = "TestDisplay", Command = "check" };
        //    commandHandler.GetCommandDefaultName("check").Returns("check");

        //    // Act
        //    await loyaltyFeature.OnCommand(new object(), commandEventArgs);

        //    // Assert
        //    await serviceBackbone.Received(1).SendChatMessage("TestDisplay", "TestTarget has no pasties or doesn't exist.");

        //}

        //[Fact]
        //public async Task OnCommand_CheckPasties_HasPasties()
        //{
        //    // Arrange
        //    var viewerPoint = new ViewerPoint { Points = 1000 };
        //    var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
        //    dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);
        //    var commandEventArgs = new CommandEventArgs { Args = new List<string> { "TestTarget" }, TargetUser = "TestTarget", DisplayName = "TestDisplay", Command = "check" };
        //    commandHandler.GetCommandDefaultName("check").Returns("check");

        //    // Act
        //    await loyaltyFeature.OnCommand(new object(), commandEventArgs);

        //    // Assert
        //    await serviceBackbone.Received(1).SendChatMessage("TestDisplay", "TestTarget has 1,000 pasties.");

        //}

        //[Fact]
        //public async Task CheckUsersPasties_ShouldThrowSkipCooldownException_WhenArgsCountIsLessThan2()
        //{
        //    // Arrange
        //    var commandEventArgs = new CommandEventArgs
        //    {
        //        Command = "check",
        //        Args = [],
        //        DisplayName = "testDisplayName"
        //    };
        //    commandHandler.GetCommandDefaultName("check").Returns("check");

        //    // Act & Act
        //    await Assert.ThrowsAsync<SkipCooldownException>(() =>  loyaltyFeature.OnCommand(new object(), commandEventArgs));
        //    await serviceBackbone.Received(1).SendChatMessage("testDisplayName", "to check Pasties the command is !check USERNAME");
        //}

        //[Fact]
        //public async Task OnCommand_Gift_CantGiftToSelf()
        //{
        //    // Arrange
        //    var viewerPoint = new ViewerPoint { Points = 100 };
        //    var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
        //    var targetViewerPoint = new ViewerPoint { Points = 10 };
        //    var targetQueryable = new List<ViewerPoint> { targetViewerPoint }.AsQueryable().BuildMockDbSet();
        //    dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable, targetQueryable);

        //    var commandEventArgs = new CommandEventArgs
        //    {
        //        TargetUser = "TestName",
        //        DisplayName = "TestDisplay",
        //        Command = "gift",
        //        Name = "TestName",
        //        Args = new List<string> { "testuser", "10" }
        //    };
        //    commandHandler.GetCommandDefaultName("gift").Returns("gift");

        //    viewerFeature.GetViewerByUserName("TestTarget").Returns(new Viewer());

        //    // Act


        //    // Assert
        //    await Assert.ThrowsAsync<SkipCooldownException>(async () => await loyaltyFeature.OnCommand(this, commandEventArgs));
        //}

        //[Fact]
        //public async Task OnCommand_Gift_ShouldThrowBadAmount()
        //{
        //    // Arrange
        //    var viewerPoint = new ViewerPoint { Points = 100 };
        //    var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
        //    var targetViewerPoint = new ViewerPoint { Points = 10 };
        //    var targetQueryable = new List<ViewerPoint> { targetViewerPoint }.AsQueryable().BuildMockDbSet();
        //    dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable, targetQueryable);

        //    var commandEventArgs = new CommandEventArgs
        //    {
        //        TargetUser = "TestTarget",
        //        DisplayName = "TestDisplay",
        //        Command = "gift",
        //        Name = "TestName",
        //        Args = new List<string> { "testuser", "bad" }
        //    };
        //    commandHandler.GetCommandDefaultName("gift").Returns("gift");

        //    viewerFeature.GetViewerByUserName("TestTarget").Returns(new Viewer());

        //    // Act


        //    // Assert
        //    await Assert.ThrowsAsync<SkipCooldownException>(async () => await loyaltyFeature.OnCommand(this, commandEventArgs));
        //}

        //[Fact]
        //public async Task OnCommand_Gift_ShouldThrowNoViewer()
        //{
        //    // Arrange
        //    var viewerPoint = new ViewerPoint { Points = 100 };
        //    var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
        //    var targetViewerPoint = new ViewerPoint { Points = 10 };
        //    var targetQueryable = new List<ViewerPoint> { targetViewerPoint }.AsQueryable().BuildMockDbSet();
        //    dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable, targetQueryable);

        //    var commandEventArgs = new CommandEventArgs
        //    {
        //        TargetUser = "TestTarget",
        //        DisplayName = "TestDisplay",
        //        Command = "gift",
        //        Name = "TestName",
        //        Args = new List<string> { "testuser", "10" }
        //    };
        //    commandHandler.GetCommandDefaultName("gift").Returns("gift");

        //    // Act


        //    // Assert
        //    await Assert.ThrowsAsync<SkipCooldownException>(async () => await loyaltyFeature.OnCommand(this, commandEventArgs));
        //}

        //[Fact]
        //public async Task OnCommand_Gift_ShouldThrowNotEnough()
        //{
        //    // Arrange
        //    var viewerPoint = new ViewerPoint { Points = 1 };
        //    var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
        //    var targetViewerPoint = new ViewerPoint { Points = 10 };
        //    var targetQueryable = new List<ViewerPoint> { targetViewerPoint }.AsQueryable().BuildMockDbSet();
        //    dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable, targetQueryable);

        //    var commandEventArgs = new CommandEventArgs
        //    {
        //        TargetUser = "TestTarget",
        //        DisplayName = "TestDisplay",
        //        Command = "gift",
        //        Name = "TestName",
        //        Args = new List<string> { "testuser", "10" }
        //    };
        //    commandHandler.GetCommandDefaultName("gift").Returns("gift");

        //    viewerFeature.GetViewerByUserName("TestTarget").Returns(new Viewer());

        //    // Act


        //    // Assert
        //    await Assert.ThrowsAsync<SkipCooldownException>(async () => await loyaltyFeature.OnCommand(this, commandEventArgs));
        //}

        //[Fact]
        //public async Task OnCommand_Pasties_ShouldSayPasties()
        //{
        //    // Arrange
        //    var viewerPointWithRank = new ViewerPointWithRank { Points = 1 };
        //    var pointQueryable = new List<ViewerPointWithRank> { viewerPointWithRank }.AsQueryable().BuildMockDbSet();
        //    dbContext.ViewerPointWithRanks.Find(x => true).ReturnsForAnyArgs(pointQueryable);

        //    var viewerTimeWithRank = new ViewerTimeWithRank();
        //    var timeQueryable = new List<ViewerTimeWithRank> { viewerTimeWithRank }.AsQueryable().BuildMockDbSet();
        //    dbContext.ViewersTimeWithRank.Find(x => true).ReturnsForAnyArgs(timeQueryable);

        //    var viewerMessageCountWithRank = new ViewerMessageCountWithRank();
        //    var messageQueryable = new List<ViewerMessageCountWithRank> { viewerMessageCountWithRank }.AsQueryable().BuildMockDbSet();
        //    dbContext.ViewerMessageCountsWithRank.Find(x => true).ReturnsForAnyArgs(messageQueryable);

        //    var commandEventArgs = new CommandEventArgs
        //    {
        //        TargetUser = "TestTarget",
        //        DisplayName = "TestDisplay",
        //        Command = "pasties",
        //        Name = "TestName"
        //    };
        //    commandHandler.GetCommandDefaultName("pasties").Returns("pasties");
        //    viewerFeature.GetNameWithTitle("TestName").Returns("TestNameWithTitle");

        //    // Act
        //    await loyaltyFeature.OnCommand(this, commandEventArgs);

        //    // Assert
        //    await serviceBackbone.Received(1).SendChatMessage("TestNameWithTitle Watch time: [0 sec] - sptvBacon Pasties: [#0, 1] - Messages: [#0, 0 Messages]");
        //}

        //[Fact]
        //public async Task OnCommand_AddPasties_ShouldAddPasties()
        //{
        //    // Arrange
        //    var viewerPoint = new ViewerPoint { Points = 10 };
        //    var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
        //    dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);
        //    viewerFeature.GetViewerByUserId("123").Returns(new Viewer());
        //    viewerFeature.GetViewerId("TestTarget").Returns("123");
        //    var commandEventArgs = new CommandEventArgs
        //    {
        //        TargetUser = "TestTarget",
        //        DisplayName = "TestDisplay",
        //        Command = "addpasties",
        //        Name = "TestName",
        //        Args = new List<string> { "targetuser", "100" },
        //        UserId = "123"
        //    };
        //    commandHandler.GetCommandDefaultName("addpasties").Returns("addpasties");

        //    // Act
        //    await loyaltyFeature.OnCommand(this, commandEventArgs);

        //    // Assert
        //    dbContext.ViewerPoints.Received(1).Update(viewerPoint);
        //    await dbContext.Received(1).SaveChangesAsync();
        //    Assert.Equal(110, viewerPoint.Points);
        //}

        [Fact]
        public async Task GetTopNLoudes_ShouldReturnList()
        {
            // Arrange
            var viewerPoint = new ViewerMessageCountWithRank { MessageCount = 10, Username = "testname" };
            var queryable = new List<ViewerMessageCountWithRank> { viewerPoint };
            dbContext.ViewerMessageCountsWithRank.GetAsync(x => true).ReturnsForAnyArgs(queryable);

            // Act
            var points = await loyaltyFeature.GetTopNLoudest(10);

            // Assert
            Assert.Contains(viewerPoint, points);
        }
    }
}
