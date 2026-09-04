using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Services;
using PenguinTwitchBot.TwitchApi.EventSub;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Channel;
using PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;
using PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.EventSub;
using Xunit;

namespace PenguinTwitchBot.Test.Services
{
    public class RaidRewardServiceTests
    {
        private sealed class ConcreteEventSubMetadata : EventSubMetadata { }

        private readonly IServiceBackbone _serviceBackbone = Substitute.For<IServiceBackbone>();
        private readonly IEventSubWebsocketClient _eventSubClient = Substitute.For<IEventSubWebsocketClient>();
        private readonly IViewerFeature _viewerFeature = Substitute.For<IViewerFeature>();
        private readonly IPointsSystem _pointsSystem = Substitute.For<IPointsSystem>();
        private readonly ITwitchService _twitchService = Substitute.For<ITwitchService>();
        private readonly IRaidRewardSettingsService _settings = Substitute.For<IRaidRewardSettingsService>();
        private readonly IModerationClient _moderationClient = Substitute.For<IModerationClient>();
        private readonly IConfiguration _configuration;
        private readonly RaidRewardService _service;

        public RaidRewardServiceTests()
        {
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "twitchClientId", "client-id" },
                    { "twitchAccessToken", "token" }
                })
                .Build();

            _eventSubClient.SessionId.Returns("session-1");
            _twitchService.GetBroadcasterUserId().Returns("broadcaster-1");
            _moderationClient.CreateEventSubSubscriptionDetailedAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Any<Dictionary<string, string>>(), Arg.Any<EventSubTransportMethod>(), Arg.Any<string>())
                .Returns(new CreateEventSubSubscriptionResult(true, "sub-1", null));

            _service = new RaidRewardService(
                Substitute.For<ILogger<RaidRewardService>>(),
                _serviceBackbone, _eventSubClient, _viewerFeature, _pointsSystem,
                _twitchService, _settings, _moderationClient, _configuration, TimeProvider.System);
        }

        private static RaidRewardConfig DefaultConfig() => new(
            Enabled: true,
            PointTypeId: 5,
            PointsToAward: 100,
            TimeWindowMinutes: 5,
            Message: "penguin raid",
            SubscriberMessage: "penguin sub raid",
            AnnouncementTemplate: "tpl",
            PostAnnouncement: true);

        private async Task StartAndOpenRaidWindowAsync(RaidRewardConfig config, List<string>? current = null, List<string>? active = null)
        {
            _settings.GetConfigAsync().Returns(config);
            _pointsSystem.GetPointTypeById(config.PointTypeId).Returns(new PointType { Id = config.PointTypeId, Name = "Points" });
            _viewerFeature.GetCurrentViewers().Returns(current ?? new List<string> { "viewer1" });
            _viewerFeature.GetActiveViewers().Returns(active ?? new List<string>());

            await _service.StartAsync(CancellationToken.None);

            // Invoke the outgoing raid handler directly.
            await _service.OnOutgoingRaid(_serviceBackbone, new OutgoingRaidEventArgs { TargetUserId = "target-1", TargetDisplayName = "Target", TargetUserName = "target", NumberOfViewers = 10 });
            await Task.Delay(10);
        }

        private Task SendChatMessageAsync(string login, string userId, string text, string broadcasterId = "target-1")
        {
            var args = new ChannelChatMessageEventArgs
            {
                Metadata = new ConcreteEventSubMetadata { MessageId = Guid.NewGuid().ToString(), MessageType = "notification", MessageTimestamp = DateTime.UtcNow },
                Event = new ChannelChatMessage
                {
                    ChatterUserId = userId,
                    ChatterUserLogin = login,
                    ChatterUserName = login,
                    BroadcasterUserId = broadcasterId,
                    Message = new ChatMessage { Text = text }
                }
            };
            return _service.OnChannelChatMessage(_eventSubClient, args);
        }

        [Fact]
        public async Task SharedChatGuestChannelMessage_NotAwarded()
        {
            // A message from a DIFFERENT channel in a shared-chat session (different
            // broadcaster_user_id) must not count even if the chatter is eligible.
            await StartAndOpenRaidWindowAsync(DefaultConfig());
            await SendChatMessageAsync("viewer1", "uid-1", "penguin raid", broadcasterId: "57135261");
            await Task.Delay(10);

            await _pointsSystem.DidNotReceive().AddPointsByUserId(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<long>());
        }

        [Fact]
        public async Task EligibleViewer_SendsMessage_AwardedOnce()
        {
            await StartAndOpenRaidWindowAsync(DefaultConfig());
            await SendChatMessageAsync("viewer1", "uid-1", "PENGUIN RAID hype!");
            await Task.Delay(10);

            await _pointsSystem.Received(1).AddPointsByUserId("uid-1", 5, 100);
        }

        [Fact]
        public async Task DuplicateMessage_NotAwardedTwice()
        {
            await StartAndOpenRaidWindowAsync(DefaultConfig());
            await SendChatMessageAsync("viewer1", "uid-1", "penguin raid");
            await SendChatMessageAsync("viewer1", "uid-1", "penguin raid again");
            await Task.Delay(10);

            await _pointsSystem.Received(1).AddPointsByUserId("uid-1", 5, 100);
        }

        [Fact]
        public async Task IneligibleViewer_NotAwarded()
        {
            await StartAndOpenRaidWindowAsync(DefaultConfig());
            await SendChatMessageAsync("outsider", "uid-9", "penguin raid");
            await Task.Delay(10);

            await _pointsSystem.DidNotReceive().AddPointsByUserId(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<long>());
        }

        [Fact]
        public async Task MessageWithoutPhrase_NotAwarded()
        {
            await StartAndOpenRaidWindowAsync(DefaultConfig());
            await SendChatMessageAsync("viewer1", "uid-1", "hello there");
            await Task.Delay(10);

            await _pointsSystem.DidNotReceive().AddPointsByUserId(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<long>());
        }

        [Fact]
        public async Task NonSub_UsingSubPhrase_NotAwarded()
        {
            await StartAndOpenRaidWindowAsync(DefaultConfig());
            _viewerFeature.IsSubscriber("viewer1").Returns(false);
            await SendChatMessageAsync("viewer1", "uid-1", "penguin sub raid");
            await Task.Delay(10);

            await _pointsSystem.DidNotReceive().AddPointsByUserId(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<long>());
        }

        [Fact]
        public async Task Sub_UsingSubPhrase_Awarded()
        {
            await StartAndOpenRaidWindowAsync(DefaultConfig());
            _viewerFeature.IsSubscriber("viewer1").Returns(true);
            await SendChatMessageAsync("viewer1", "uid-1", "penguin sub raid!!");
            await Task.Delay(10);

            await _pointsSystem.Received(1).AddPointsByUserId("uid-1", 5, 100);
        }

        [Fact]
        public async Task DisabledFeature_NoSubscriptionCreated()
        {
            var config = DefaultConfig() with { Enabled = false };
            _settings.GetConfigAsync().Returns(config);
            await _service.StartAsync(CancellationToken.None);

            await _service.OnOutgoingRaid(_serviceBackbone, new OutgoingRaidEventArgs { TargetUserId = "t", TargetDisplayName = "T", TargetUserName = "t" });
            await Task.Delay(10);

            await _moderationClient.DidNotReceive().CreateEventSubSubscriptionDetailedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(), Arg.Any<EventSubTransportMethod>(), Arg.Any<string>());
        }
    }
}
