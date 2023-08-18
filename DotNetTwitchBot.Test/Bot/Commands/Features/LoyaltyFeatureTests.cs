using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.Repository;
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

            // Act
            serviceBackbone.SubscriptionEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<SubscriptionEventArgs>>(this, subscriptionEvent);

            // Assert
            await ticketFeature.Received(1).GiveTicketsToViewer("testname", 50);
            await serviceBackbone.Received(1).SendChatMessage(Arg.Is<string>(x => x.Contains(" just subscribed sptvHype")));

        }

        [Fact]
        public async Task OnSubscription_ShouldGiveTickets_Renew()
        {
            // Arrange
            var subscriptionEvent = new SubscriptionEventArgs { Name = "testname", DisplayName = "TestName", Count = 12 };

            // Act
            serviceBackbone.SubscriptionEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<SubscriptionEventArgs>>(this, subscriptionEvent);

            // Assert
            await ticketFeature.Received(1).GiveTicketsToViewer("testname", 50);
            await serviceBackbone.Received(1).SendChatMessage(Arg.Is<string>(x => x.Contains(" just subscribed for")));

        }


        [Fact]
        public async Task OnChangeMessage_ShouldUpdateMessageCount()
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
        public async Task OnChangeMessage_ShouldCreateMessageCount()
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
    }
}
