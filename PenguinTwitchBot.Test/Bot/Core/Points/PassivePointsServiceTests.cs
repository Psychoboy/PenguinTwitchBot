using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Database.Bot.Models.Points;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Core.Points
{
    public class PassivePointsServiceTests
    {
        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _utcNow = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => _utcNow;

            public void AdvanceMinutes(int minutes) => _utcNow = _utcNow.AddMinutes(minutes);
        }

        private readonly ILogger<PassivePointsService> _logger = Substitute.For<ILogger<PassivePointsService>>();
        private readonly IServiceBackbone _serviceBackbone = Substitute.For<IServiceBackbone>();
        private readonly IPointsSystem _pointsSystem = Substitute.For<IPointsSystem>();
        private readonly IViewerFeature _viewerFeature = Substitute.For<IViewerFeature>();
        private readonly IFeatureRuntimeCoordinator _featureRuntimeCoordinator = Substitute.For<IFeatureRuntimeCoordinator>();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly PassivePointsService _service;

        public PassivePointsServiceTests()
        {
            _serviceBackbone.IsOnline.Returns(true);
            _serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);
            _featureRuntimeCoordinator.IsEnabled(FeatureKeys.PassivePoints).Returns(true);

            _service = new PassivePointsService(
                _logger,
                _serviceBackbone,
                _pointsSystem,
                _viewerFeature,
                _featureRuntimeCoordinator,
                _timeProvider);
        }

        [Fact]
        public async Task ProcessTickAsync_DoesNothing_WhenStreamOffline()
        {
            _serviceBackbone.IsOnline.Returns(false);

            await _service.ProcessTickAsync();

            await _pointsSystem.DidNotReceive().GetPointTypes();
            await _pointsSystem.DidNotReceiveWithAnyArgs().AddPointsByUserId(default!, default, default);
        }

        [Fact]
        public async Task DistributeForPointTypeAsync_StacksAdditiveRewards_AndSubscribersAlwaysActive()
        {
            var pointTypeId = Guid.CreateVersion7();
            var pointType = new PointType
            {
                Id = pointTypeId,
                Name = "Tickets",
                PassivePointsEnabled = true,
                PassivePointsIntervalMinutes = 5,
                PassivePointsAllViewers = 2,
                PassivePointsActiveViewers = 3,
                PassivePointsSubscribers = 5
            };

            // Viewers in chat: lurker, chatter, sub_lurker, sub_chatter, nightbot
            _viewerFeature.GetCurrentViewers().Returns(["lurker", "chatter", "sub_lurker", "sub_chatter", "nightbot"]);
            // Active viewers (only chatter and sub_chatter recently spoke, sub_lurker has not spoken)
            _viewerFeature.GetActiveViewers().Returns(["chatter", "sub_chatter"]);

            _viewerFeature.IsSubscriber("lurker").Returns(false);
            _viewerFeature.IsSubscriber("chatter").Returns(false);
            _viewerFeature.IsSubscriber("sub_lurker").Returns(true);
            _viewerFeature.IsSubscriber("sub_chatter").Returns(true);

            _viewerFeature.GetViewerId("lurker").Returns("id_lurker");
            _viewerFeature.GetViewerId("chatter").Returns("id_chatter");
            _viewerFeature.GetViewerId("sub_lurker").Returns("id_sub_lurker");
            _viewerFeature.GetViewerId("sub_chatter").Returns("id_sub_chatter");
            _viewerFeature.GetViewerId("nightbot").Returns("id_nightbot");

            _serviceBackbone.IsKnownBot("nightbot").Returns(true);

            await _service.DistributeForPointTypeAsync(pointType);

            // Lurker: AllViewers = 2
            await _pointsSystem.Received(1).AddPointsByUserId("id_lurker", pointTypeId, 2);

            // Active non-sub chatter: AllViewers (2) + ActiveViewers (3) = 5
            await _pointsSystem.Received(1).AddPointsByUserId("id_chatter", pointTypeId, 5);

            // Sub who is lurking in chat: Subscribers in chat are ALWAYS considered active -> All (2) + Active (3) + Sub (5) = 10
            await _pointsSystem.Received(1).AddPointsByUserId("id_sub_lurker", pointTypeId, 10);

            // Sub who is chatting: All (2) + Active (3) + Sub (5) = 10
            await _pointsSystem.Received(1).AddPointsByUserId("id_sub_chatter", pointTypeId, 10);

            // Known bot excluded
            await _pointsSystem.DidNotReceive().AddPointsByUserId("id_nightbot", Arg.Any<Guid>(), Arg.Any<long>());
        }

        [Fact]
        public async Task ProcessTickAsync_RespectsPerPointTypeInterval_AndEnabledFlag()
        {
            var pt1Id = Guid.CreateVersion7();
            var pt2Id = Guid.CreateVersion7();
            var pt3Id = Guid.CreateVersion7();
            var pt1 = new PointType
            {
                Id = pt1Id,
                Name = "Loyalty",
                PassivePointsEnabled = true,
                PassivePointsIntervalMinutes = 2,
                PassivePointsAllViewers = 10,
                PassivePointsActiveViewers = 0,
                PassivePointsSubscribers = 0
            };

            var pt2 = new PointType
            {
                Id = pt2Id,
                Name = "Tickets",
                PassivePointsEnabled = true,
                PassivePointsIntervalMinutes = 3,
                PassivePointsAllViewers = 5,
                PassivePointsActiveViewers = 5,
                PassivePointsSubscribers = 10
            };

            var pt3Disabled = new PointType
            {
                Id = pt3Id,
                Name = "DisabledCurrency",
                PassivePointsEnabled = false,
                PassivePointsIntervalMinutes = 1,
                PassivePointsAllViewers = 100
            };

            _pointsSystem.GetPointTypes().Returns([pt1, pt2, pt3Disabled]);
            _viewerFeature.GetCurrentViewers().Returns(["viewer1"]);
            _viewerFeature.GetActiveViewers().Returns([]);
            _viewerFeature.IsSubscriber("viewer1").Returns(false);
            _viewerFeature.GetViewerId("viewer1").Returns("id_viewer1");

            // Initial tick (t=0): initializes _lastAwardedUtc baseline for enabled point types
            await _service.ProcessTickAsync();
            await _pointsSystem.DidNotReceiveWithAnyArgs().AddPointsByUserId(default!, default, default);

            // t=1 min: neither pt1 (interval 2) nor pt2 (interval 3) fires
            _timeProvider.AdvanceMinutes(1);
            await _service.ProcessTickAsync();
            await _pointsSystem.DidNotReceiveWithAnyArgs().AddPointsByUserId(default!, default, default);

            // t=2 min: pt1 fires (2 min elapsed), pt2 not yet (2 < 3)
            _timeProvider.AdvanceMinutes(1);
            await _service.ProcessTickAsync();
            await _pointsSystem.Received(1).AddPointsByUserId("id_viewer1", pt1Id, 10);
            await _pointsSystem.DidNotReceive().AddPointsByUserId("id_viewer1", pt2Id, Arg.Any<long>());

            // t=3 min: pt2 fires (3 min elapsed), pt1 at 1 min since last payout
            _timeProvider.AdvanceMinutes(1);
            await _service.ProcessTickAsync();
            await _pointsSystem.Received(1).AddPointsByUserId("id_viewer1", pt2Id, 5);

            // Disabled currency never fires
            await _pointsSystem.DidNotReceive().AddPointsByUserId("id_viewer1", pt3Id, Arg.Any<long>());
        }

        [Fact]
        public void PassivePointsTemplateReplacer_ReplacesVariablesAndComputesMathAutomatically()
        {
            var pointType = new PointType
            {
                Id = Guid.CreateVersion7(),
                Name = "Tickets",
                PassivePointsEnabled = true,
                PassivePointsIntervalMinutes = 5,
                PassivePointsAllViewers = 1,
                PassivePointsActiveViewers = 2,
                PassivePointsSubscribers = 3
            };

            var template = "Earn {PointType} every {Interval} mins! " +
                           "Lurkers: {AllViewers} ({AllViewersPerHour}/hr), " +
                           "Active: {ActiveTotal} ({ActiveTotalPerHour}/hr, +{ActiveViewers} bonus), " +
                           "Subs: {SubTotal} ({SubTotalPerHour}/hr, +{Subscribers} bonus). " +
                           "Entry cost: {PointsPerEntry}.";

            var result = PassivePointsTemplateReplacer.ReplaceVariables(template, pointType, pointsPerEntry: 10);

            // Interval = 5 mins (12 intervals/hr)
            // AllViewers = 1 -> 12/hr
            // ActiveTotal = 1 + 2 = 3 -> 36/hr
            // SubTotal = 1 + 2 + 3 = 6 -> 72/hr
            Assert.Equal(
                "Earn Tickets every 5 mins! Lurkers: 1 (12/hr), Active: 3 (36/hr, +2 bonus), Subs: 6 (72/hr, +3 bonus). Entry cost: 10.",
                result);
        }

        [Fact]
        public void PassivePointsTemplateReplacer_DoesNotRescanReplacementValuesForOtherTokens()
        {
            var pointType = new PointType
            {
                Id = Guid.CreateVersion7(),
                Name = "{Interval} {SubTotal}",
                PassivePointsEnabled = true,
                PassivePointsIntervalMinutes = 5,
                PassivePointsAllViewers = 1,
                PassivePointsActiveViewers = 2,
                PassivePointsSubscribers = 3
            };

            var result = PassivePointsTemplateReplacer.ReplaceVariables("Currency: {pointtype}, Interval: {INTERVAL}", pointType);

            Assert.Equal("Currency: {Interval} {SubTotal}, Interval: 5", result);
        }
    }
}

