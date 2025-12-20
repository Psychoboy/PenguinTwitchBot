using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Models.Points;
using DotNetTwitchBot.CustomMiddleware;
using MediatR;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Core.Points
{
    public class TwitchEventsBonusTests
    {
        private readonly ILogger<TwitchEventsBonus> _logger;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly ICommandHandler _commandHandler;
        private readonly IGameSettingsService _gameSettingsService;
        private readonly IPointsSystem _pointsSystem;
        private readonly IMediator mediatorSubstitute;
        private readonly TwitchEventsBonus _twitchEventsBonus;

        public TwitchEventsBonusTests()
        {
            _logger = Substitute.For<ILogger<TwitchEventsBonus>>();
            _serviceBackbone = Substitute.For<IServiceBackbone>();
            _commandHandler = Substitute.For<ICommandHandler>();
            _gameSettingsService = Substitute.For<IGameSettingsService>();
            _pointsSystem = Substitute.For<IPointsSystem>();
            mediatorSubstitute = Substitute.For<IMediator>();

            _twitchEventsBonus = new TwitchEventsBonus(
                _logger,
                _serviceBackbone,
                _commandHandler,
                _gameSettingsService,
                mediatorSubstitute,
                _pointsSystem
            );
        }

        [Fact]
        public async Task Register_ShouldRegisterDefaultPointForGame()
        {
            await _twitchEventsBonus.Register();

            await _pointsSystem.Received(1).RegisterDefaultPointForGame("TwitchEventBonus");
        }

        [Fact]
        public async Task OnCheer_ShouldAwardPointsForCheer()
        {
            var cheerEventArgs = new CheerEventArgs
            {
                Name = "TestUser",
                UserId = "user123",
                DisplayName = "TestUser",
                Amount = 100,
                IsAnonymous = false
            };

            _gameSettingsService.GetDoubleSetting("TwitchEventBonus", "BitsPerPoint", 1.0).Returns(1.0);
            _pointsSystem.GetPointTypeForGame("TwitchEventBonus").Returns(new PointType { Name = "Points" });
            await _twitchEventsBonus.StartAsync(default);

            _serviceBackbone.CheerEvent += Raise.Event<AsyncEventHandler<CheerEventArgs>>(this, cheerEventArgs);

            await _pointsSystem.Received(1).AddPointsByUserIdAndGame("user123", "TwitchEventBonus", 100);
            await _serviceBackbone.Received(1).SendChatMessage("TestUser just cheered 100 bits! sptvHype");
        }

        [Fact]
        public async Task OnSubscription_ShouldAwardPointsForSubscription()
        {
            var subscriptionEventArgs = new SubscriptionEventArgs
            {
                Name = "TestUser",
                UserId = "user123",
                DisplayName = "TestUser",
                Count = 1,
                Streak = 1,
                IsGift = false
            };

            _gameSettingsService.GetIntSetting("TwitchEventBonus", "PointsPerSub", 500).Returns(500);
            _pointsSystem.GetPointTypeForGame("TwitchEventBonus").Returns(new PointType { Name = "Points" });

            await _twitchEventsBonus.StartAsync(default);

            _serviceBackbone.SubscriptionEvent += Raise.Event<AsyncEventHandler<SubscriptionEventArgs>>(this, subscriptionEventArgs);
            //await _twitchEventsBonus.OnSubscription(this, subscriptionEventArgs);

            await _pointsSystem.Received(1).AddPointsByUserIdAndGame("user123", "TwitchEventBonus", 500);
            await _serviceBackbone.Received(1).SendChatMessage("TestUser just subscribed for a total of 1 months and for 1 months in a row! sptvHype");
        }

        [Fact]
        public async Task OnSubscriptionGift_ShouldAwardPointsForSubscriptionGift()
        {
            var subscriptionGiftEventArgs = new SubscriptionGiftEventArgs
            {
                Name = "TestUser",
                UserId = "user123",
                DisplayName = "TestUser",
                GiftAmount = 5,
                TotalGifted = 10
            };

            _gameSettingsService.GetIntSetting("TwitchEventBonus", "PointsPerSub", 500).Returns(500);
            _pointsSystem.GetPointTypeForGame("TwitchEventBonus").Returns(new PointType { Name = "Points" });
            await _twitchEventsBonus.StartAsync(default);

            _serviceBackbone.SubscriptionGiftEvent += Raise.Event<AsyncEventHandler<SubscriptionGiftEventArgs>>(this, subscriptionGiftEventArgs);
            //await _twitchEventsBonus.OnSubScriptionGift(this, subscriptionGiftEventArgs);

            await _pointsSystem.Received(1).AddPointsByUserIdAndGame("user123", "TwitchEventBonus", 2500);
            await _serviceBackbone.Received(1).SendChatMessage("TestUser gifted 5 subscriptions to the channel! sptvHype sptvHype sptvHype They have gifted a total of 10 subs to the channel!");
        }

        [Fact]
        public async Task SetPointsPerSub_ShouldSaveSetting()
        {
            await _twitchEventsBonus.SetPointsPerSub(1000);

            await _gameSettingsService.Received(1).SaveSetting("TwitchEventBonus", "PointsPerSub", 1000);
        }

        [Fact]
        public async Task GetPointsPerSub_ShouldReturnSetting()
        {
            _gameSettingsService.GetIntSetting("TwitchEventBonus", "PointsPerSub", 500).Returns(1000);

            var result = await _twitchEventsBonus.GetPointsPerSub();

            Assert.Equal(1000, result);
        }

        [Fact]
        public async Task SetBitsPerPoint_ShouldSaveSetting()
        {
            await _twitchEventsBonus.SetBitsPerPoint(10.0);

            await _gameSettingsService.Received(1).SaveSetting("TwitchEventBonus", "BitsPerPoint", 10.0);
        }

        [Fact]
        public async Task GetBitsPerPoint_ShouldReturnSetting()
        {
            _gameSettingsService.GetDoubleSetting("TwitchEventBonus", "BitsPerPoint", 1.0).Returns(10.0);

            var result = await _twitchEventsBonus.GetBitsPerPoint();

            Assert.Equal(10.0, result);
        }

        [Fact]
        public async Task SetPointType_ShouldSavePointType()
        {
            var pointType = new PointType { Id = 1, Name = "Points" };

            await _twitchEventsBonus.SetPointType(pointType);

            await _pointsSystem.Received(1).SetPointTypeForGame("TwitchEventBonus", 1);
        }

        [Fact]
        public async Task GetPointType_ShouldReturnPointType()
        {
            var pointType = new PointType { Id = 1, Name = "Points" };
            _pointsSystem.GetPointTypeForGame("TwitchEventBonus").Returns(pointType);

            var result = await _twitchEventsBonus.GetPointType();

            Assert.Equal(pointType, result);
        }
    }
}


