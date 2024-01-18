using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Repository;
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

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            loyaltyFeature = new LoyaltyFeature(logger, viewerFeature, scopeFactory, serviceBackbone, ticketFeature, commandHandler);
        }


        [Fact]
        public async Task OnCheer_Anonymous()
        {
            // Arrange
            var cheerEventArgs = new CheerEventArgs { IsAnonymous = true, Amount = 100 };

            // Act
            serviceBackbone.CheerEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<CheerEventArgs>>(this, cheerEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("Someone just cheered 100 bits! sptvHype");
        }

        [Fact]
        public async Task OnCheer_ShouldGiveTickets()
        {
            // Arrange
            var cheerEventArgs = new CheerEventArgs { DisplayName = "TestName", Name = "testname", IsAnonymous = false, Amount = 100 };
            var queryable = new List<Setting> { }.AsQueryable().BuildMockDbSet();
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(queryable);
            // Act
            serviceBackbone.CheerEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<CheerEventArgs>>(this, cheerEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("TestName just cheered 100 bits! sptvHype");
            await ticketFeature.Received(1).GiveTicketsToViewer("testname", 10);
        }


        [Fact]
        public async Task OnSubscriptionGift_ShouldGiveTickets_AndSayTotalGiven()
        {
            // Arrange
            var subscriptionEvent = new SubscriptionGiftEventArgs { Name = "testname", DisplayName = "TestName", GiftAmount = 5, TotalGifted = 10 };
            var queryable = new List<Setting> { }.AsQueryable().BuildMockDbSet();
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            serviceBackbone.SubscriptionGiftEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<SubscriptionGiftEventArgs>>(this, subscriptionEvent);

            // Assert
            await ticketFeature.Received(1).GiveTicketsToViewer("testname", 250);
            await serviceBackbone.Received(1).SendChatMessage("TestName gifted 5 subscriptions to the channel! sptvHype sptvHype sptvHype They have gifted a total of 10 subs to the channel!");

        }

        [Fact]
        public async Task OnSubscriptionGift_ShouldGiveTickets_JustBaseMessage()
        {
            // Arrange
            var subscriptionEvent = new SubscriptionGiftEventArgs { Name = "testname", DisplayName = "TestName", GiftAmount = 5, TotalGifted = 5 };
            var queryable = new List<Setting> { }.AsQueryable().BuildMockDbSet();
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            serviceBackbone.SubscriptionGiftEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<SubscriptionGiftEventArgs>>(this, subscriptionEvent);

            // Assert
            await ticketFeature.Received(1).GiveTicketsToViewer("testname", 250);
            await serviceBackbone.Received(1).SendChatMessage("TestName gifted 5 subscriptions to the channel! sptvHype sptvHype sptvHype");

        }


        [Fact]
        public async Task OnSubscription_ShouldGiveTickets()
        {
            // Arrange
            var subscriptionEvent = new SubscriptionEventArgs { Name = "testname", DisplayName = "TestName" };
            var queryable = new List<Setting> { }.AsQueryable().BuildMockDbSet();
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            serviceBackbone.SubscriptionEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<SubscriptionEventArgs>>(this, subscriptionEvent);

            // Assert
            await ticketFeature.Received(1).GiveTicketsToViewer("testname", 50);
            await serviceBackbone.Received(1).SendChatMessage(Arg.Is<string>(x => x.Contains(" just subscribed! sptvHype")));

        }

        [Fact]
        public async Task OnSubscription_ShouldSayTotalCount()
        {
            // Arrange
            var subscriptionEvent = new SubscriptionEventArgs { Name = "testname", DisplayName = "TestName", Count = 5 };
            var queryable = new List<Setting> { }.AsQueryable().BuildMockDbSet();
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            serviceBackbone.SubscriptionEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<SubscriptionEventArgs>>(this, subscriptionEvent);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage(Arg.Is<string>(x => x.Contains(" just subscribed for a total of 5 months! sptvHype")));

        }

        [Fact]
        public async Task OnSubscription_ShouldSayCumalativeCount()
        {
            // Arrange
            var subscriptionEvent = new SubscriptionEventArgs { Name = "testname", DisplayName = "TestName", Streak = 5 };
            var queryable = new List<Setting> { }.AsQueryable().BuildMockDbSet();
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            serviceBackbone.SubscriptionEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<SubscriptionEventArgs>>(this, subscriptionEvent);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage(Arg.Is<string>(x => x.Contains(" just subscribed for 5 months in a row! sptvHype")));

        }

        [Fact]
        public async Task OnSubscription_ShouldSayTotalAndCumalativeCount()
        {
            // Arrange
            var subscriptionEvent = new SubscriptionEventArgs { Name = "testname", DisplayName = "TestName", Count = 8, Streak = 5 };
            var queryable = new List<Setting> { }.AsQueryable().BuildMockDbSet();
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            serviceBackbone.SubscriptionEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<SubscriptionEventArgs>>(this, subscriptionEvent);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage(Arg.Is<string>(x => x.Contains(" just subscribed for a total of 8 months and for 5 months in a row! sptvHype")));

        }

        [Fact]
        public async Task OnSubscription_ShouldGiveTickets_Renew()
        {
            // Arrange
            var subscriptionEvent = new SubscriptionEventArgs { Name = "testname", DisplayName = "TestName", Count = 12 };
            var queryable = new List<Setting> { }.AsQueryable().BuildMockDbSet();
            dbContext.Settings.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            serviceBackbone.SubscriptionEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<SubscriptionEventArgs>>(this, subscriptionEvent);

            // Assert
            await ticketFeature.Received(1).GiveTicketsToViewer("testname", 50);
            await serviceBackbone.Received(1).SendChatMessage(Arg.Is<string>(x => x.Contains(" just subscribed for")));

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
            serviceBackbone.ChatMessageEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<ChatMessageEventArgs>>(this, chatMessageEvent);

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
            serviceBackbone.ChatMessageEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<ChatMessageEventArgs>>(this, chatMessageEvent);

            // Assert
            dbContext.ViewerMessageCounts.Received(1).Update(Arg.Any<ViewerMessageCount>());
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task AddPointsToViewer_ShouldCreateViewer()
        {
            // Arrange
            var queryable = new List<ViewerPoint> { }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            await loyaltyFeature.AddPointsToViewer("test", 5);

            // Assert
            dbContext.ViewerPoints.Received(1).Update(Arg.Any<ViewerPoint>());
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task AddPointsToViewer_ShouldUpdateViewer()
        {
            // Arrange
            var viewerPoint = new ViewerPoint();
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            await loyaltyFeature.AddPointsToViewer("test", 5);

            // Assert
            dbContext.ViewerPoints.Received(1).Update(viewerPoint);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task UpdatePointsAndTime_ShouldGivePointsAndUpdateTime()
        {
            // Arrange
            var queryable = new List<ViewerPoint> { }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);
            var timeQuerable = new List<ViewerTime> { }.AsQueryable().BuildMockDbSet();
            dbContext.ViewersTime.Find(x => true).ReturnsForAnyArgs(timeQuerable);
            viewerFeature.GetCurrentViewers().Returns(new List<string> { "test" });
            viewerFeature.GetActiveViewers().Returns(new List<string> { "test" });
            serviceBackbone.IsKnownBot("test").Returns(false);
            serviceBackbone.IsOnline = true;

            // Act
            await loyaltyFeature.UpdatePointsAndTime();

            // Assert
            dbContext.ViewerPoints.Received(2).Update(Arg.Any<ViewerPoint>());
            await dbContext.Received(3).SaveChangesAsync();
        }


        [Fact]
        public async Task OnCommand_CheckPasties_NoPasties()
        {
            // Arrange
            var viewerPoint = new ViewerPoint();
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);
            var commandEventArgs = new CommandEventArgs { TargetUser = "TestTarget", DisplayName = "TestDisplay", Command = "check" };
            commandHandler.GetCommandDefaultName("check").Returns("check");

            // Act
            await loyaltyFeature.OnCommand(new object(), commandEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("TestDisplay", "TestTarget has no pasties or doesn't exist.");

        }

        [Fact]
        public async Task OnCommand_CheckPasties_HasPasties()
        {
            // Arrange
            var viewerPoint = new ViewerPoint { Points = 1000 };
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);
            var commandEventArgs = new CommandEventArgs { TargetUser = "TestTarget", DisplayName = "TestDisplay", Command = "check" };
            commandHandler.GetCommandDefaultName("check").Returns("check");

            // Act
            await loyaltyFeature.OnCommand(new object(), commandEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("TestDisplay", "TestTarget has 1,000 pasties.");

        }

        [Fact]
        public async Task OnCommand_Gift_ShouldGiftPasaties()
        {
            // Arrange
            var viewerPoint = new ViewerPoint { Points = 100 };
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            var targetViewerPoint = new ViewerPoint { Points = 10 };
            var targetQueryable = new List<ViewerPoint> { targetViewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable, targetQueryable);

            var commandEventArgs = new CommandEventArgs
            {
                TargetUser = "TestTarget",
                DisplayName = "TestDisplay",
                Command = "gift",
                Name = "TestName",
                Args = new List<string> { "testuser", "10" }
            };
            commandHandler.GetCommandDefaultName("gift").Returns("gift");

            viewerFeature.GetViewer("TestTarget").Returns(new Viewer());

            // Act
            await loyaltyFeature.OnCommand(this, commandEventArgs);

            // Assert
            dbContext.ViewerPoints.Received(1).Update(viewerPoint);
            dbContext.ViewerPoints.Received(1).Update(viewerPoint);
            Assert.Equal(90, viewerPoint.Points);
            Assert.Equal(20, targetViewerPoint.Points);
            await dbContext.Received(2).SaveChangesAsync();
        }

        [Fact]
        public async Task OnCommand_Gift_CantGiftToSelf()
        {
            // Arrange
            var viewerPoint = new ViewerPoint { Points = 100 };
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            var targetViewerPoint = new ViewerPoint { Points = 10 };
            var targetQueryable = new List<ViewerPoint> { targetViewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable, targetQueryable);

            var commandEventArgs = new CommandEventArgs
            {
                TargetUser = "TestName",
                DisplayName = "TestDisplay",
                Command = "gift",
                Name = "TestName",
                Args = new List<string> { "testuser", "10" }
            };
            commandHandler.GetCommandDefaultName("gift").Returns("gift");

            viewerFeature.GetViewer("TestTarget").Returns(new Viewer());

            // Act


            // Assert
            await Assert.ThrowsAsync<SkipCooldownException>(async () => await loyaltyFeature.OnCommand(this, commandEventArgs));
        }

        [Fact]
        public async Task OnCommand_Gift_ShouldThrowBadAmount()
        {
            // Arrange
            var viewerPoint = new ViewerPoint { Points = 100 };
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            var targetViewerPoint = new ViewerPoint { Points = 10 };
            var targetQueryable = new List<ViewerPoint> { targetViewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable, targetQueryable);

            var commandEventArgs = new CommandEventArgs
            {
                TargetUser = "TestTarget",
                DisplayName = "TestDisplay",
                Command = "gift",
                Name = "TestName",
                Args = new List<string> { "testuser", "bad" }
            };
            commandHandler.GetCommandDefaultName("gift").Returns("gift");

            viewerFeature.GetViewer("TestTarget").Returns(new Viewer());

            // Act


            // Assert
            await Assert.ThrowsAsync<SkipCooldownException>(async () => await loyaltyFeature.OnCommand(this, commandEventArgs));
        }

        [Fact]
        public async Task OnCommand_Gift_ShouldThrowNoViewer()
        {
            // Arrange
            var viewerPoint = new ViewerPoint { Points = 100 };
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            var targetViewerPoint = new ViewerPoint { Points = 10 };
            var targetQueryable = new List<ViewerPoint> { targetViewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable, targetQueryable);

            var commandEventArgs = new CommandEventArgs
            {
                TargetUser = "TestTarget",
                DisplayName = "TestDisplay",
                Command = "gift",
                Name = "TestName",
                Args = new List<string> { "testuser", "10" }
            };
            commandHandler.GetCommandDefaultName("gift").Returns("gift");

            // Act


            // Assert
            await Assert.ThrowsAsync<SkipCooldownException>(async () => await loyaltyFeature.OnCommand(this, commandEventArgs));
        }

        [Fact]
        public async Task OnCommand_Gift_ShouldThrowNotEnough()
        {
            // Arrange
            var viewerPoint = new ViewerPoint { Points = 1 };
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            var targetViewerPoint = new ViewerPoint { Points = 10 };
            var targetQueryable = new List<ViewerPoint> { targetViewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable, targetQueryable);

            var commandEventArgs = new CommandEventArgs
            {
                TargetUser = "TestTarget",
                DisplayName = "TestDisplay",
                Command = "gift",
                Name = "TestName",
                Args = new List<string> { "testuser", "10" }
            };
            commandHandler.GetCommandDefaultName("gift").Returns("gift");

            viewerFeature.GetViewer("TestTarget").Returns(new Viewer());

            // Act


            // Assert
            await Assert.ThrowsAsync<SkipCooldownException>(async () => await loyaltyFeature.OnCommand(this, commandEventArgs));
        }

        [Fact]
        public async Task OnCommand_Pasties_ShouldSayPasties()
        {
            // Arrange
            var viewerPointWithRank = new ViewerPointWithRank { Points = 1 };
            var pointQueryable = new List<ViewerPointWithRank> { viewerPointWithRank }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPointWithRanks.Find(x => true).ReturnsForAnyArgs(pointQueryable);

            var viewerTimeWithRank = new ViewerTimeWithRank();
            var timeQueryable = new List<ViewerTimeWithRank> { viewerTimeWithRank }.AsQueryable().BuildMockDbSet();
            dbContext.ViewersTimeWithRank.Find(x => true).ReturnsForAnyArgs(timeQueryable);

            var viewerMessageCountWithRank = new ViewerMessageCountWithRank();
            var messageQueryable = new List<ViewerMessageCountWithRank> { viewerMessageCountWithRank }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerMessageCountsWithRank.Find(x => true).ReturnsForAnyArgs(messageQueryable);

            var commandEventArgs = new CommandEventArgs
            {
                TargetUser = "TestTarget",
                DisplayName = "TestDisplay",
                Command = "pasties",
                Name = "TestName"
            };
            commandHandler.GetCommandDefaultName("pasties").Returns("pasties");
            viewerFeature.GetNameWithTitle("TestName").Returns("TestNameWithTitle");

            // Act
            await loyaltyFeature.OnCommand(this, commandEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("TestNameWithTitle Watch time: [0 sec] - sptvBacon Pasties: [#0, 1] - Messages: [#0, 0 Messages]");
        }

        [Fact]
        public async Task OnCommand_AddPasties_ShouldAddPasties()
        {
            // Arrange
            var viewerPoint = new ViewerPoint { Points = 10 };
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);
            var commandEventArgs = new CommandEventArgs
            {
                TargetUser = "TestTarget",
                DisplayName = "TestDisplay",
                Command = "addpasties",
                Name = "TestName",
                Args = new List<string> { "targetuser", "100" }
            };
            commandHandler.GetCommandDefaultName("addpasties").Returns("addpasties");

            // Act
            await loyaltyFeature.OnCommand(this, commandEventArgs);

            // Assert
            dbContext.ViewerPoints.Received(1).Update(viewerPoint);
            await dbContext.Received(1).SaveChangesAsync();
            Assert.Equal(110, viewerPoint.Points);
        }

        [Fact]
        public async Task GetUserPasties_ShouldReturnPoints()
        {
            // Arrange
            var viewerPoint = new ViewerPoint { Points = 10, Username = "testname" };
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            var points = await loyaltyFeature.GetUserPasties("testname");

            // Assert
            Assert.Equal(viewerPoint, points);
        }

        [Fact]
        public async Task GetUserPasties_NewViewer_ShouldReturnNewPoints()
        {
            // Arrange
            var queryable = new List<ViewerPoint> { }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            var points = await loyaltyFeature.GetUserPasties("testname");

            // Assert
            Assert.Equal(0, points.Points);
        }

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


        [Fact]
        public async Task GetMaxPointsFromUser_ShouldReturnMax()
        {
            // Arrange
            var viewerPoint = new ViewerPoint { Points = LoyaltyFeature.MaxBet + 10, Username = "testname" };
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            var points = await loyaltyFeature.GetMaxPointsFromUser("testname");

            // Assert
            Assert.Equal(LoyaltyFeature.MaxBet, points);

        }

        [Fact]
        public async Task GetMaxPointsFromUser_ShouldReturnPoints()
        {
            // Arrange
            var viewerPoint = new ViewerPoint { Points = 10, Username = "testname" };
            var queryable = new List<ViewerPoint> { viewerPoint }.AsQueryable().BuildMockDbSet();
            dbContext.ViewerPoints.Find(x => true).ReturnsForAnyArgs(queryable);

            // Act
            var points = await loyaltyFeature.GetMaxPointsFromUser("testname");

            // Assert
            Assert.Equal(10, points);

        }
    }
}
