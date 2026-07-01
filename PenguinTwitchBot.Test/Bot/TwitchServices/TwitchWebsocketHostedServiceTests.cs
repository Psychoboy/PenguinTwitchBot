using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Chat;
using PenguinTwitchBot.Bot.Services.Chat;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.TwitchApi.EventSub;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Channel;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Stream;
using PenguinTwitchBot.TwitchApi.EventSub.Models.Bits;
using PenguinTwitchBot.TwitchApi.EventSub.Models.ChannelPoints;
using PenguinTwitchBot.TwitchApi.EventSub.Models.ChannelSuspiciousUser;
using PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;
using PenguinTwitchBot.TwitchApi.EventSub.Models.Charity;
using PenguinTwitchBot.TwitchApi.EventSub.Models.Subscriptions;
using PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;
using PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Stream;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets;
using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;
using Xunit;

#pragma warning disable NS1001, NS1004, NS5000

namespace PenguinTwitchBot.Test.Bot.TwitchServices;

public class TwitchWebsocketHostedServiceTests
{
    private readonly TwitchWebsocketHostedService _service;
    private readonly ILogger<TwitchWebsocketHostedService> _logger;
    private readonly IServiceBackbone _eventService;
    private readonly IEventSubWebsocketClient _eventSubWebsocketClient;
    private readonly ISubscriptionTracker _subscriptionHistory;
    private readonly IChatMessageIdTracker _messageIdTracker;
    private readonly IMemoryCache _memoryCache;
    private readonly ITwitchService _twitchService;
    private readonly TimeProvider _timeProvider;
    private readonly IPenguinDispatcher _dispatcher;
    private readonly ITwitchEventActionHandler _twitchEventActionHandler;
    private readonly IChatColorService _chatColorService;

    public TwitchWebsocketHostedServiceTests()
    {
        _logger = Substitute.For<ILogger<TwitchWebsocketHostedService>>();
        _eventService = Substitute.For<IServiceBackbone>();
        _eventSubWebsocketClient = Substitute.For<IEventSubWebsocketClient>();
        _subscriptionHistory = Substitute.For<ISubscriptionTracker>();
        _messageIdTracker = Substitute.For<IChatMessageIdTracker>();
        _memoryCache = Substitute.For<IMemoryCache>();
        _twitchService = Substitute.For<ITwitchService>();
        _timeProvider = TimeProvider.System;
        _dispatcher = Substitute.For<IPenguinDispatcher>();
        _twitchEventActionHandler = Substitute.For<ITwitchEventActionHandler>();
        _chatColorService = Substitute.For<IChatColorService>();

        _service = new(
            _logger, _eventService, _eventSubWebsocketClient, _subscriptionHistory,
            _messageIdTracker, _memoryCache, _twitchService, _timeProvider,
            _dispatcher, _twitchEventActionHandler, _chatColorService);
    }

    private static EventSubMetadata CreateMetadata(string messageId = "msg-1", string messageType = "notification")
    {
        return new ConcreteEventSubMetadata { MessageId = messageId, MessageType = messageType, MessageTimestamp = DateTime.UtcNow };
    }

    private sealed class ConcreteEventSubMetadata : EventSubMetadata
    {
    }

    private static ChannelChatMessageEventArgs CreateChannelChatMessageArgs(
        string messageId = "msg-1", string text = "Hello", string userId = "uid-1",
        string userName = "TestUser", string userLogin = "testuser", string? rewardId = null,
        string? sourceBroadcasterUserId = null)
    {
        return new ChannelChatMessageEventArgs
        {
            Metadata = CreateMetadata(messageId),
            Event = new ChannelChatMessage
            {
                MessageId = messageId, ChatterUserId = userId, ChatterUserName = userName,
                ChatterUserLogin = userLogin, Message = new ChatMessage { Text = text },
                Badges = [], Color = "#FF0000",
                ChannelPointsCustomRewardId = rewardId,
                SourceBroadcasterUserId = sourceBroadcasterUserId
            }
        };
    }

    private static ChannelChatNotificationEventArgs CreateChannelChatNotificationArgs(string messageId = "msg-1", bool populateOptionals = true)
    {
        return new ChannelChatNotificationEventArgs
        {
            Metadata = CreateMetadata(messageId),
            Event = new ChannelChatNotification
            {
                MessageId = messageId,
                ChatterUserId = "uid-1",
                ChatterUserName = "TestUser",
                ChatterUserLogin = "testuser",
                Message = null!,
                NoticeType = "sub",
                Sub = populateOptionals ? new ChatSub { SubTier = "1000", DurationMonths = 3, IsPrime = true } : null,
                Resub = populateOptionals ? new ChatResub { CumulativeMonths = 12, DurationMonths = 3, StreakMonths = 6, SubTier = "2000", IsPrime = true, IsGift = false, GifterIsAnonymous = false, GifterUserId = "gifter-1", GifterUserName = "Gifter", GifterUserLogin = "gifter" } : null,
                SubGift = populateOptionals ? new ChatSubGift { DurationMonths = 3, CumulativeTotal = 5, RecipientUserId = "rec-1", RecipientUserName = "Recipient", RecipientUserLogin = "recipient", SubTier = "1000", CommunityGiftId = "cg-1" } : null,
                CommunitySubGift = populateOptionals ? new ChatCommunitySubGift { Id = "csg-1", Total = 10, SubTier = "1000", CumulativeTotal = 20 } : null,
                GiftPaidUpgrade = populateOptionals ? new ChatGiftPaidUpgrade { GifterIsAnonymous = false, GifterUserId = "gifter-1", GifterUserName = "Gifter", GifterUserLogin = "gifter" } : null,
                PrimePaidUpgrade = populateOptionals ? new ChatPrimePaidUpgrade { SubTier = "1000" } : null,
                Raid = populateOptionals ? new ChatRaid { UserId = "raid-1", UserName = "Raider", UserLogin = "raider", ViewerCount = 42, ProfileImageUrl = "url" } : null,
                PayItForward = populateOptionals ? new ChatPayItForward { GifterIsAnonymous = false, GifterUserId = "gifter-1", GifterUserName = "Gifter", GifterUserLogin = "gifter", RecipientUserId = "rec-1", RecipientUserName = "Recipient", RecipientUserLogin = "recipient" } : null,
                Announcement = populateOptionals ? new ChatAnnouncement { Color = "blue" } : null,
                CharityDonation = populateOptionals ? new ChatCharityDonation { Name = "Charity", Amount = new CharityAmount { Value = 550, DecimalPlaces = 2, Currency = "USD" } } : null,
                BitsBadgeTier = populateOptionals ? new ChannelBitsBadgeTier { Tier = 1000 } : null,
                WatchStreak = populateOptionals ? new WatchStreak { StreakCount = 5, ChannelPointsAwarded = 100 } : null,
            }
        };
    }

    private static ChannelChatMessageDeleteEventArgs CreateChannelChatMessageDeleteArgs(string messageId = "msg-1")
    {
        return new ChannelChatMessageDeleteEventArgs
        {
            Metadata = CreateMetadata(messageId),
            Event = new ChannelChatMessageDelete
            {
                MessageId = messageId, TargetUserId = "uid-target",
                TargetUserName = "TargetUser", TargetUserLogin = "targetuser"
            }
        };
    }

    private static ChannelSuspiciousUserMessageEventArgs CreateSuspiciousUserMessageArgs(string messageId = "msg-1")
    {
        return new ChannelSuspiciousUserMessageEventArgs
        {
            Metadata = CreateMetadata(messageId),
            Event = new ChannelSuspiciousUserMessage
            {
                Message = new SuspiciousUserMessage { MessageId = messageId, Text = "Hello" },
                UserId = "uid-1", UserName = "TestUser", UserLogin = "testuser"
            }
        };
    }

    private static ChannelAdBreakBeginEventArgs CreateChannelAdBreakBeginArgs()
    {
        return new ChannelAdBreakBeginEventArgs
        {
            Metadata = CreateMetadata("msg-1"),
            Event = new ChannelAdBreakBegin
            {
                DurationSeconds = 30, StartedAt = DateTimeOffset.UtcNow, IsAutomatic = true
            }
        };
    }

    private static ChannelUnbanEventArgs CreateChannelUnbanArgs()
    {
        return new ChannelUnbanEventArgs
        {
            Metadata = CreateMetadata("msg-1"),
            Event = new ChannelUnban
            {
                UserId = "uid-1", UserLogin = "testuser", UserName = "TestUser",
                ModeratorUserId = "mod-1", ModeratorUserLogin = "moderator", ModeratorUserName = "Moderator"
            }
        };
    }

    private static ChannelBanEventArgs CreateChannelBanArgs(bool isPermanent = true)
    {
        return new ChannelBanEventArgs
        {
            Metadata = CreateMetadata("msg-1"),
            Event = new ChannelBan
            {
                UserId = "uid-1", UserLogin = "testuser", UserName = "TestUser",
                IsPermanent = isPermanent, Reason = "spam", BannedAt = DateTimeOffset.UtcNow,
                EndsAt = isPermanent ? null : DateTimeOffset.UtcNow.AddMinutes(10),
                ModeratorUserId = "mod-1", ModeratorUserLogin = "moderator", ModeratorUserName = "Moderator"
            }
        };
    }

    private static ChannelRaidEventArgs CreateChannelRaidArgs()
    {
        return new ChannelRaidEventArgs
        {
            Metadata = CreateMetadata("msg-1"),
            Event = new ChannelRaid
            {
                FromBroadcasterUserId = "uid-raider", FromBroadcasterUserName = "Raider",
                FromBroadcasterUserLogin = "raider", Viewers = 42
            }
        };
    }

    private static ChannelSubscribeEventArgs CreateChannelSubscribeArgs(bool isGift = false)
    {
        return new ChannelSubscribeEventArgs
        {
            Metadata = CreateMetadata("msg-1"),
            Event = new ChannelSubscribe
            {
                UserId = "uid-1", UserLogin = "testuser", UserName = "TestUser",
                IsGift = isGift, Tier = "1000"
            }
        };
    }

    private static ChannelSubscriptionMessageEventArgs CreateChannelSubscriptionRenewalArgs()
    {
        return new ChannelSubscriptionMessageEventArgs
        {
            Metadata = CreateMetadata("msg-1"),
            Event = new ChannelSubscriptionMessage
            {
                UserId = "uid-1", UserLogin = "testuser", UserName = "TestUser",
                CumulativeMonths = 12, StreakMonths = 6, Tier = "1000",
                Message = new SubscriptionMessage { Text = "Great stream!" }
            }
        };
    }

    private static ChannelSubscriptionGiftEventArgs CreateChannelSubscriptionGiftArgs()
    {
        return new ChannelSubscriptionGiftEventArgs
        {
            Metadata = CreateMetadata("msg-1"),
            Event = new ChannelSubscriptionGift
            {
                UserId = "uid-1", UserLogin = "testuser", UserName = "TestUser",
                Total = 5, CumulativeTotal = 10
            }
        };
    }

    private static ChannelSubscriptionEndEventArgs CreateChannelSubscriptionEndArgs()
    {
        return new ChannelSubscriptionEndEventArgs
        {
            Metadata = CreateMetadata("msg-1"),
            Event = new ChannelSubscriptionEnd { UserId = "uid-1", UserLogin = "testuser" }
        };
    }

    private static ChannelCheerEventArgs CreateChannelCheerArgs()
    {
        return new ChannelCheerEventArgs
        {
            Metadata = CreateMetadata("msg-1"),
            Event = new ChannelCheer
            {
                UserId = "uid-1", UserLogin = "testuser", UserName = "TestUser",
                Bits = 100, Message = "Cheer!", IsAnonymous = false
            }
        };
    }

    private static ChannelPointsCustomRewardRedemptionEventArgs CreateChannelPointRedeemedArgs(string? userInput = null)
    {
        return new ChannelPointsCustomRewardRedemptionEventArgs
        {
            Metadata = CreateMetadata("msg-1"),
            Event = new ChannelPointsCustomRewardRedemption
            {
                UserId = "uid-1", UserLogin = "testuser", UserName = "TestUser",
                UserInput = userInput ?? string.Empty,
                Reward = new RedemptionReward { Id = "reward-1", Title = "Test Reward" },
                Status = "fulfilled"
            }
        };
    }

    private static ChannelBitsUseEventArgs CreateChannelBitsUseArgs(string messageId = "msg-1", bool populateOptionals = true)
    {
        return new ChannelBitsUseEventArgs
        {
            Metadata = CreateMetadata(messageId),
            Event = new ChannelBitsUse
            {
                UserId = "uid-1", UserLogin = "testuser", UserName = "TestUser",
                Bits = 100, Type = "type1",
                BroadcasterUserId = "uid-bc", BroadcasterUserLogin = "broadcaster", BroadcasterUserName = "Broadcaster",
                Message = populateOptionals ? new PenguinTwitchBot.TwitchApi.EventSub.Models.Bits.BitsMessage { Text = "Bits message", Fragments = new PenguinTwitchBot.TwitchApi.EventSub.Models.Bits.BitsMessageFragments[] { new PenguinTwitchBot.TwitchApi.EventSub.Models.Bits.BitsMessageFragments { Type = "text", Text = "Bits message", Emote = new PenguinTwitchBot.TwitchApi.EventSub.Models.Bits.BitsEmote { Id = "emote-1", EmoteSetId = "set-1", OwnerId = "owner-1", Format = new[] { "static" } } } } } : null,
                PowerUp = populateOptionals ? new PenguinTwitchBot.TwitchApi.EventSub.Models.Bits.PowerUp { Type = "type1", Emote = new PenguinTwitchBot.TwitchApi.EventSub.Models.Bits.PowerUpEmote { Id = "emote-1" } } : null,
                CustomPowerUp = populateOptionals ? new PenguinTwitchBot.TwitchApi.EventSub.Models.Bits.CustomPowerUp { Title = "Custom", RewardId = "reward-1" } : null
            }
        };
    }

    private static ChannelFollowEventArgs CreateChannelFollowArgs()
    {
        return new ChannelFollowEventArgs
        {
            Metadata = CreateMetadata("msg-1"),
            Event = new ChannelFollow
            {
                UserId = "uid-1", UserLogin = "testuser", UserName = "TestUser",
                FollowedAt = DateTimeOffset.UtcNow
            }
        };
    }

    [Fact]
    public void Constructor_InitializesDefaults()
    {
        Assert.NotNull(_service);
    }

    [Fact]
    public async Task AdBreak_CallsEventService()
    {
        var args = new AdBreakStartEventArgs { Automatic = true, Length = 30, StartedAt = DateTimeOffset.UtcNow };
        await _service.AdBreak(args);
        await _eventService.Received(1).OnAdBreakStartEvent(args);
    }

    [Fact]
    public async Task StreamOffline_CallsEventServiceAndHandler()
    {
        _twitchService.IsStreamOnline().Returns(false);
        await _service.StreamOffline();
        Assert.False(_eventService.IsOnline);
        await _eventService.Received(1).OnStreamEnded();
        await _twitchEventActionHandler.Received(1).HandleStreamOfflineAsync();
    }

    [Fact]
    public async Task StreamOffline_WhenThrows_LogsError()
    {
        _eventService.When(x => x.OnStreamEnded()).Do(x => throw new InvalidOperationException("test"));
        await _service.StreamOffline();
        _logger.Received(1).LogError(Arg.Any<Exception>(), "Error in websocket message");
    }

    [Fact]
    public async Task StreamOnline_CallsEventServiceAndHandler()
    {
        _twitchService.IsStreamOnline().Returns(true);
        await _service.StreamOnline();
        Assert.True(_eventService.IsOnline);
        await _eventService.Received(1).OnStreamStarted();
        await _twitchEventActionHandler.Received(1).HandleStreamOnlineAsync();
    }

    [Fact]
    public async Task StreamOnline_WhenThrows_LogsError()
    {
        _eventService.When(x => x.OnStreamStarted()).Do(x => throw new InvalidOperationException("test"));
        await _service.StreamOnline();
        _logger.Received(1).LogError(Arg.Any<Exception>(), "Error in websocket message");
    }

    [Fact]
    public async Task Reconnect_WhenCancelled_ReturnsImmediately()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await _service.Reconnect(cts.Token);
    }

    [Fact]
    public async Task Reconnect_WhenAlreadyReconnecting_Returns()
    {
        typeof(TwitchWebsocketHostedService)
            .GetField("Reconnecting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_service, true);
        await _service.Reconnect();

    }

    [Fact]
    public async Task Reconnect_WhenReconnectSucceeds_Returns()
    {
        _twitchService.ValidateAndRefreshToken().Returns(true);
        _eventSubWebsocketClient.ReconnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        await _service.Reconnect();
        await _twitchService.Received(1).ValidateAndRefreshToken();
        await _eventSubWebsocketClient.Received(1).ReconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DidProcessMessage_WhenCacheMiss_AddsToCacheAndReturnsFalse()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        bool result = _service.DidProcessMessage(CreateMetadata("msg-1"));
        Assert.False(result);
        _memoryCache.Received(1).Set("msg-1", "msg-1", TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task DidProcessMessage_WhenCacheHit_ReturnsTrue()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(x =>
        {
            x[1] = "cached";
            return true;
        });
        bool result = _service.DidProcessMessage(CreateMetadata("msg-1"));
        Assert.True(result);
    }

    [Fact]
    public async Task ChannelChatMessage_WhenSelfMessage_ReturnsEarly()
    {
        _messageIdTracker.IsSelfMessage("msg-1").Returns(true);
        await _service.ChannelChatMessage(this, CreateChannelChatMessageArgs("msg-1"));
        await _dispatcher.DidNotReceive().Publish(Arg.Any<ReceivedChatMessage>());
    }

    [Fact]
    public async Task ChannelChatMessage_WithChannelPointReward_CallsRewardAndPublishes()
    {
        _messageIdTracker.IsSelfMessage("msg-1").Returns(false);
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        _twitchService.GetCustomReward("reward-1").Returns(new ChannelPointReward("reward-1", "Test Reward", true, false, 100, null, true, null, false, false, 1, false, 0, false, 1));
        await _service.ChannelChatMessage(this, CreateChannelChatMessageArgs("msg-1", rewardId: "reward-1"));
        await _twitchService.Received(1).GetCustomReward("reward-1");
        await _eventService.Received(1).OnChannelPointRedeem("uid-1", "testuser", "Test Reward", "Hello");
        await _twitchEventActionHandler.Received(1).HandleChannelPointRedemptionAsync(Arg.Is<ChannelPointRedeemEventArgs>(e =>
            e.UserId == "uid-1" && e.Sender == "TestUser" && e.Title == "Test Reward"));
    }

    [Fact]
    public async Task ChannelChatMessage_WithChannelPointReward_NullReward_Returns()
    {
        _messageIdTracker.IsSelfMessage("msg-1").Returns(false);
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        _twitchService.GetCustomReward("reward-1").Returns((ChannelPointReward?)null!);
        await _service.ChannelChatMessage(this, CreateChannelChatMessageArgs("msg-1", rewardId: "reward-1"));

    }

    [Fact]
    public async Task ChannelChatMessage_NormalChat_PublishesChatMessage()
    {
        _messageIdTracker.IsSelfMessage("msg-1").Returns(false);
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.ChannelChatMessage(this, CreateChannelChatMessageArgs("msg-1", "!hello"));
        await _eventService.Received(1).OnCommand(Arg.Any<CommandEventArgs>());
        await _dispatcher.Received(1).Publish(Arg.Is<ReceivedChatMessage>(m =>
            m.EventArgs != null && m.EventArgs.Message == "!hello"));
    }

    [Fact]
    public async Task ChannelChatMessageDelete_PublishesDeletedMessage()
    {
        var args = CreateChannelChatMessageDeleteArgs("msg-1");
        await _service.ChannelChatMessageDelete(this, args);
        await _dispatcher.Received(1).Publish(Arg.Is<DeletedChatMessage>(m => m.EventArgs == args));
    }

    [Fact]
    public async Task ChannelSuspiciousUserMessage_WhenValid_PublishesReceivedChatMessage()
    {
        var args = CreateSuspiciousUserMessageArgs("msg-1");
        _messageIdTracker.IsSelfMessage("msg-1").Returns(false);
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.ChannelSuspiciousUserMessage(this, args);
        await _dispatcher.Received(1).Publish(Arg.Is<ReceivedChatMessage>(m =>
            m.EventArgs != null && m.EventArgs.UserId == "uid-1" && m.EventArgs.Name == "testuser"));
    }

    [Fact]
    public async Task ChannelAdBreakBegin_WhenValid_CallsHandlerAndEventService()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.ChannelAdBreakBegin(this, CreateChannelAdBreakBeginArgs());
        await _twitchEventActionHandler.Received(1).HandleAdBreakBeginAsync(Arg.Is<AdBreakStartEventArgs>(e =>
            e.Automatic == true && e.Length == 30));
        await _eventService.Received(1).OnAdBreakStartEvent(Arg.Is<AdBreakStartEventArgs>(e =>
            e.Automatic == true && e.Length == 30));
    }

    [Fact]
    public async Task OnChannelUnBan_WhenValid_CallsEventServiceAndHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnChannelUnBan(this, CreateChannelUnbanArgs());
        await _eventService.Received(1).OnViewerBan("uid-1", "testuser", true, null);
        await _twitchEventActionHandler.Received(1).HandleChannelUnbanAsync(Arg.Is<BanEventArgs>(e =>
            e.UserId == "uid-1" && e.Name == "testuser" && e.IsUnBan == true));
    }

    [Fact]
    public async Task OnChannelBan_WithTimeout_PublishesBanned()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnChannelBan(this, CreateChannelBanArgs(isPermanent: false));
        await _dispatcher.Received(1).Publish(Arg.Is<BannedChatUser>(m => m.UserId == "uid-1"));
        await _eventService.DidNotReceive().OnViewerBan(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<DateTimeOffset?>());
    }

    [Fact]
    public async Task OnChannelBan_WithPermanentBan_CallsHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnChannelBan(this, CreateChannelBanArgs(isPermanent: true));
        await _dispatcher.Received(1).Publish(Arg.Is<BannedChatUser>(m => m.UserId == "uid-1"));
        await _eventService.Received(1).OnViewerBan("uid-1", "testuser", false, Arg.Any<DateTimeOffset?>());
        await _twitchEventActionHandler.Received(1).HandleChannelBanAsync(Arg.Is<BanEventArgs>(e =>
            e.UserId == "uid-1" && e.Name == "testuser" && e.IsPermanent == true && e.IsUnBan == false));
    }

    [Fact]
    public async Task OnChannelRaid_WhenValid_CallsEventServiceAndHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnChannelRaid(this, CreateChannelRaidArgs());
        await _eventService.Received(1).OnIncomingRaid(Arg.Is<RaidEventArgs>(e =>
            e.Name == "raider" && e.UserId == "uid-raider" && e.DisplayName == "Raider" && e.NumberOfViewers == 42));
        await _twitchEventActionHandler.Received(1).HandleRaidAsync(Arg.Any<RaidEventArgs>());
    }

    [Fact]
    public async Task OnChannelFollow_WhenValid_CallsEventServiceAndHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnChannelFollow(this, CreateChannelFollowArgs());
        await _eventService.Received(1).OnFollow(Arg.Any<ChannelFollow>());
        await _twitchEventActionHandler.Received(1).HandleFollowAsync(Arg.Is<FollowEventArgs>(e =>
            e.Username == "testuser" && e.UserId == "uid-1" && e.DisplayName == "TestUser"));
    }

    [Fact]
    public async Task OnChannelSubscription_WhenValid_CallsEventServiceAndHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        _subscriptionHistory.ExistingSub("testuser").Returns(false);
        await _service.OnChannelSubscription(this, CreateChannelSubscribeArgs());
        await _eventService.Received(1).OnSubscription(Arg.Is<SubscriptionEventArgs>(e =>
            e.Name == "testuser" && e.UserId == "uid-1" && e.IsGift == false));
        await _twitchEventActionHandler.Received(1).HandleSubscribeAsync(Arg.Any<SubscriptionEventArgs>());
    }

    [Fact]
    public async Task OnChannelSubscriptionRenewal_WhenValid_CallsEventServiceAndHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        _subscriptionHistory.ExistingSub("testuser").Returns(false);
        await _service.OnChannelSubscriptionRenewal(this, CreateChannelSubscriptionRenewalArgs());
        await _subscriptionHistory.Received(1).AddOrUpdateSubHistory("testuser", "uid-1");
        await _eventService.Received(1).OnSubscription(Arg.Is<SubscriptionEventArgs>(e =>
            e.Name == "testuser" && e.Count == 12 && e.Streak == 6 && e.IsRenewal == true));
        await _twitchEventActionHandler.Received(1).HandleSubscribeAsync(Arg.Any<SubscriptionEventArgs>());
    }

    [Fact]
    public async Task OnChannelSubscriptionGift_WhenValid_CallsEventServiceAndHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnChannelSubscriptionGift(this, CreateChannelSubscriptionGiftArgs());
        await _eventService.Received(1).OnSubscriptionGift(Arg.Is<SubscriptionGiftEventArgs>(e =>
            e.Name == "testuser" && e.UserId == "uid-1" && e.GiftAmount == 5));
        await _twitchEventActionHandler.Received(1).HandleSubscriptionGiftAsync(Arg.Any<SubscriptionGiftEventArgs>());
    }

    [Fact]
    public async Task OnChannelSubscriptionEnd_WhenValid_CallsEventServiceAndHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnChannelSubscriptionEnd(this, CreateChannelSubscriptionEndArgs());
        await _eventService.Received(1).OnSubscriptionEnd("testuser", "uid-1");
        await _twitchEventActionHandler.Received(1).HandleSubscriptionEndAsync(Arg.Is<SubscriptionEndEventArgs>(e =>
            e.Name == "testuser" && e.UserId == "uid-1"));
    }

    [Fact]
    public async Task OnChannelCheer_WhenValid_CallsEventServiceAndHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnChannelCheer(this, CreateChannelCheerArgs());
        await _eventService.Received(1).OnCheer(Arg.Any<ChannelCheer>());
        await _twitchEventActionHandler.Received(1).HandleCheerAsync(Arg.Is<CheerEventArgs>(e =>
            e.UserId == "uid-1" && e.Name == "testuser" && e.Amount == 100));
    }

    [Fact]
    public async Task OnChannelBitsUse_WhenAlreadyProcessed_ReturnsEarly()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(x =>
        {
            x[1] = "cached";
            return true;
        });
        await _service.OnChannelBitsUse(this, CreateChannelBitsUseArgs("msg-1"));
        await _twitchEventActionHandler.DidNotReceive().HandleBitsUseAsync(Arg.Any<BitsUseEventArgs>());
    }

    [Fact]
    public async Task OnChannelBitsUse_WhenValid_CallsHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnChannelBitsUse(this, CreateChannelBitsUseArgs("msg-1", populateOptionals: true));
        await _twitchEventActionHandler.Received(1).HandleBitsUseAsync(Arg.Is<BitsUseEventArgs>(e =>
            e.UserId == "uid-1" && e.Name == "testuser" && e.Amount == 100 &&
            e.IsPowerUp == true && e.PowerUp != null && e.PowerUp.Type == "type1" &&
            e.IsCustomPowerUp == true && e.CustomPowerUp != null && e.CustomPowerUp.Title == "Custom" &&
            e.HasBitsMessage == true));
    }

    [Fact]
    public async Task OnChannelBitsUse_WithNullOptionals_CallsHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnChannelBitsUse(this, CreateChannelBitsUseArgs("msg-1", populateOptionals: false));
        await _twitchEventActionHandler.Received(1).HandleBitsUseAsync(Arg.Is<BitsUseEventArgs>(e =>
            e.IsPowerUp == false && e.PowerUp == null &&
            e.IsCustomPowerUp == false && e.CustomPowerUp == null &&
            e.HasBitsMessage == false && e.BitsMessage == null));
    }

    [Fact]
    public async Task OnChannelBitsUse_WhenThrows_LogsError()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        _twitchEventActionHandler.When(x => x.HandleBitsUseAsync(Arg.Any<BitsUseEventArgs>())).Do(x => throw new InvalidOperationException("test"));
        await _service.OnChannelBitsUse(this, CreateChannelBitsUseArgs("msg-1"));
        _logger.Received(1).LogError(Arg.Any<Exception>(), "Error in websocket message");
    }

    [Fact]
    public async Task OnChannelPointRedeemed_WhenHasUserInput_ReturnsEarly()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        var args = CreateChannelPointRedeemedArgs("some input");
        await _service.OnChannelPointRedeemed(this, args);
        await _eventService.DidNotReceive().OnChannelPointRedeem(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task OnChannelPointRedeemed_WhenValid_CallsEventServiceAndHandler()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        var args = CreateChannelPointRedeemedArgs(null);
        await _service.OnChannelPointRedeemed(this, args);
        await _eventService.Received(1).OnChannelPointRedeem("uid-1", "TestUser", "Test Reward");
        await _twitchEventActionHandler.Received(1).HandleChannelPointRedemptionAsync(Arg.Is<ChannelPointRedeemEventArgs>(e =>
            e.UserId == "uid-1" && e.Sender == "TestUser" && e.Title == "Test Reward"));
    }

    [Fact]
    public async Task OnChannelChatNotification_WhenSelfMessage_ReturnsEarly()
    {
        _messageIdTracker.IsSelfMessage("msg-1").Returns(true);
        await _service.OnChannelChatNotification(this, CreateChannelChatNotificationArgs("msg-1"));
        await _twitchEventActionHandler.DidNotReceive().HandleChatNotificationAsync(Arg.Any<ChatNotificationEventArgs>());
    }

    [Fact]
    public async Task OnChannelChatNotification_WhenAlreadyProcessed_ReturnsEarly()
    {
        _messageIdTracker.IsSelfMessage("msg-1").Returns(false);
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(x =>
        {
            x[1] = "cached";
            return true;
        });
        await _service.OnChannelChatNotification(this, CreateChannelChatNotificationArgs("msg-1"));
        await _twitchEventActionHandler.DidNotReceive().HandleChatNotificationAsync(Arg.Any<ChatNotificationEventArgs>());
    }

    [Fact]
    public async Task OnChannelChatNotification_WhenValid_CallsHandler()
    {
        _messageIdTracker.IsSelfMessage("msg-1").Returns(false);
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnChannelChatNotification(this, CreateChannelChatNotificationArgs("msg-1"));
        await _twitchEventActionHandler.Received(1).HandleChatNotificationAsync(Arg.Is<ChatNotificationEventArgs>(e =>
            e.UserId == "uid-1" && e.Name == "testuser" && e.DisplayName == "TestUser" && e.NoticeType == "sub"));
    }

    [Fact]
    public async Task OnChannelChatNotification_WhenThrows_LogsError()
    {
        _messageIdTracker.IsSelfMessage("msg-1").Returns(false);
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        _twitchEventActionHandler.When(x => x.HandleChatNotificationAsync(Arg.Any<ChatNotificationEventArgs>())).Do(x => throw new InvalidOperationException("test"));
        await _service.OnChannelChatNotification(this, CreateChannelChatNotificationArgs("msg-1"));
        _logger.Received(1).LogError(Arg.Any<Exception>(), "Error processing chat notification");
    }

    [Fact]
    public async Task OnChannelChatNotification_AllOptionalFieldsNull_CallsHandler()
    {
        _messageIdTracker.IsSelfMessage("msg-1").Returns(false);
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        var args = CreateChannelChatNotificationArgs("msg-1", populateOptionals: false);
        await _service.OnChannelChatNotification(this, args);
        await _twitchEventActionHandler.Received(1).HandleChatNotificationAsync(Arg.Is<ChatNotificationEventArgs>(e =>
            e.UserId == "uid-1" && e.Name == "testuser" && e.DisplayName == "TestUser" &&
            e.Sub == null && e.Resub == null && e.SubGift == null && e.CommunitySubGift == null &&
            e.GiftPaidUpgrade == null && e.PrimePaidUpgrade == null && e.Raid == null &&
            e.PayItForward == null && e.Announcement == null && e.CharityDonation == null &&
            e.BitsBadgeTier == null && e.WatchStreak == null));
    }

    [Fact]
    public async Task OnChannelChatNotification_AllOptionalFieldsPopulated_CallsHandler()
    {
        _messageIdTracker.IsSelfMessage("msg-1").Returns(false);
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        var args = CreateChannelChatNotificationArgs("msg-1", populateOptionals: true);
        await _service.OnChannelChatNotification(this, args);
        await _twitchEventActionHandler.Received(1).HandleChatNotificationAsync(Arg.Is<ChatNotificationEventArgs>(e =>
            e.UserId == "uid-1" && e.Name == "testuser" && e.DisplayName == "TestUser" &&
            e.Sub != null && e.Resub != null && e.SubGift != null && e.CommunitySubGift != null &&
            e.GiftPaidUpgrade != null && e.PrimePaidUpgrade != null && e.Raid != null &&
            e.PayItForward != null && e.Announcement != null && e.CharityDonation != null &&
            e.BitsBadgeTier != null && e.WatchStreak != null));
    }

    [Fact]
    public async Task ChannelSuspiciousUserMessage_WhenSelfMessage_ReturnsEarly()
    {
        var args = CreateSuspiciousUserMessageArgs("msg-1");
        _messageIdTracker.IsSelfMessage("msg-1").Returns(true);
        await _service.ChannelSuspiciousUserMessage(this, args);
        await _dispatcher.DidNotReceive().Publish(Arg.Any<ReceivedChatMessage>());
    }

    [Fact]
    public async Task ChannelSuspiciousUserMessage_WhenAlreadyProcessed_ReturnsEarly()
    {
        var args = CreateSuspiciousUserMessageArgs("msg-1");
        _messageIdTracker.IsSelfMessage("msg-1").Returns(false);
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(x =>
        {
            x[1] = "cached";
            return true;
        });
        await _service.ChannelSuspiciousUserMessage(this, args);
        await _dispatcher.DidNotReceive().Publish(Arg.Any<ReceivedChatMessage>());
    }

    [Fact]
    public async Task ProcessChatMessage_WithNullFragments_HandlesGracefully()
    {
        var e = new ChannelChatMessage
        {
            MessageId = "msg-1",
            ChatterUserId = "uid-1",
            ChatterUserLogin = "testuser",
            ChatterUserName = "TestUser",
            Message = new ChatMessage { Text = "hello", Fragments = null! },
            Badges = [],
            SourceBroadcasterUserId = null
        };
        await _service.ProcessChatMessage(e);
        await _dispatcher.Received(1).Publish(Arg.Is<ReceivedChatMessage>(m =>
            m.EventArgs != null && m.EventArgs.Fragments.Count == 0));
    }

    [Fact]
    public void MapFragment_WhenEmoteAnimated_ReturnsEmoteFragment()
    {
        var fragment = new ChatMessageFragment
        {
            Type = "emote",
            Text = ":)",
            Emote = new ChatEmote { Id = "emote-1", Format = new[] { "animated" } }
        };
        var result = TwitchWebsocketHostedService.MapFragment(fragment);
        Assert.Equal("emote", result.Type);
        Assert.Equal(":)", result.Text);
        Assert.Equal("emote-1", result.EmoteId);
        Assert.Equal("twitch", result.EmoteProvider);
        Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/emote-1/animated/dark/1.0", result.EmoteUrl);
    }

    [Fact]
    public void MapFragment_WhenEmoteStatic_ReturnsEmoteFragment()
    {
        var fragment = new ChatMessageFragment
        {
            Type = "emote",
            Text = ":)",
            Emote = new ChatEmote { Id = "emote-1", Format = new[] { "static" } }
        };
        var result = TwitchWebsocketHostedService.MapFragment(fragment);
        Assert.Equal("emote", result.Type);
        Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/emote-1/static/dark/1.0", result.EmoteUrl);
    }

    [Fact]
    public void MapFragment_WhenCheermote_ReturnsCheermoteFragment()
    {
        var fragment = new ChatMessageFragment
        {
            Type = "cheermote",
            Text = "Cheer1",
            Cheermote = new ChatCheermote { Prefix = "Cheer", Bits = 100 }
        };
        var result = TwitchWebsocketHostedService.MapFragment(fragment);
        Assert.Equal("cheermote", result.Type);
        Assert.Equal("Cheer1", result.Text);
        Assert.Equal("Cheer", result.EmoteId);
        Assert.Equal(100, result.CheerAmount);
    }

    [Fact]
    public void MapFragment_WhenEmoteWithNullFormat_ReturnsStaticFragment()
    {
        var fragment = new ChatMessageFragment
        {
            Type = "emote",
            Text = ":)",
            Emote = new ChatEmote { Id = "emote-1", Format = null! }
        };
        var result = TwitchWebsocketHostedService.MapFragment(fragment);
        Assert.Equal("emote", result.Type);
        Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/emote-1/static/dark/1.0", result.EmoteUrl);
    }

    [Fact]
    public void MapFragment_WhenText_ReturnsTextFragment()
    {
        var fragment = new ChatMessageFragment
        {
            Type = "text",
            Text = "hello"
        };
        var result = TwitchWebsocketHostedService.MapFragment(fragment);
        Assert.Equal("text", result.Type);
        Assert.Equal("hello", result.Text);
    }

    [Fact]
    public void MapFragment_WhenNullType_ReturnsTextFragment()
    {
        var fragment = new ChatMessageFragment
        {
            Type = null!,
            Text = "hello"
        };
        var result = TwitchWebsocketHostedService.MapFragment(fragment);
        Assert.Equal("text", result.Type);
        Assert.Equal("hello", result.Text);
    }

    [Fact]
    public async Task MessageReceived_UpdatesLastMessageReceived()
    {
        var before = _timeProvider.GetLocalNow();
        await _service.MessageReceived(this, new MessageReceivedEventArgs());
        var after = _timeProvider.GetLocalNow();
        var lastReceived = (DateTimeOffset)_service.GetType()
            .GetField("LastMessageReceived", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(_service)!;
        Assert.InRange(lastReceived, before, after);
    }

    [Fact]
    public async Task OnWebsocketReconnected_DoesNotThrow()
    {
        _eventSubWebsocketClient.SessionId.Returns("session-123");
        var ex = await Record.ExceptionAsync(() => _service.OnWebsocketReconnected(this, EventArgs.Empty));
        Assert.Null(ex);
    }

    private static StreamOfflineEventArgs CreateStreamOfflineArgs(string messageId = "msg-1")
    {
        return new StreamOfflineEventArgs
        {
            Metadata = CreateMetadata(messageId),
            Event = new StreamOffline()
        };
    }

    private static StreamOnlineEventArgs CreateStreamOnlineArgs(string messageId = "msg-1")
    {
        return new StreamOnlineEventArgs
        {
            Metadata = CreateMetadata(messageId),
            Event = new StreamOnline()
        };
    }

    private static ErrorOccurredEventArgs CreateErrorOccurredArgs(Exception ex)
    {
        return new ErrorOccurredEventArgs
        {
            Message = "Test error",
            Exception = ex
        };
    }

    [Fact]
    public async Task OnStreamOffline_WhenAlreadyProcessed_ReturnsEarly()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(x =>
        {
            x[1] = "cached";
            return true;
        });
        await _service.OnStreamOffline(this, CreateStreamOfflineArgs("msg-1"));
        await _eventService.DidNotReceive().OnStreamEnded();
        await _twitchEventActionHandler.DidNotReceive().HandleStreamOfflineAsync();
    }

    [Fact]
    public async Task OnStreamOffline_WhenValid_CallsStreamOffline()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnStreamOffline(this, CreateStreamOfflineArgs("msg-1"));
        await _eventService.Received(1).OnStreamEnded();
        await _twitchEventActionHandler.Received(1).HandleStreamOfflineAsync();
    }

    [Fact]
    public async Task OnStreamOnline_WhenAlreadyProcessed_ReturnsEarly()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(x =>
        {
            x[1] = "cached";
            return true;
        });
        await _service.OnStreamOnline(this, CreateStreamOnlineArgs("msg-1"));
        await _eventService.DidNotReceive().OnStreamStarted();
        await _twitchEventActionHandler.DidNotReceive().HandleStreamOnlineAsync();
    }

    [Fact]
    public async Task OnStreamOnline_WhenValid_CallsStreamOnline()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()!).Returns(false);
        await _service.OnStreamOnline(this, CreateStreamOnlineArgs("msg-1"));
        await _eventService.Received(1).OnStreamStarted();
        await _twitchEventActionHandler.Received(1).HandleStreamOnlineAsync();
    }

    [Fact]
    public async Task OnErrorOccurred_CallsReconnect()
    {
        _twitchService.ValidateAndRefreshToken().Returns(true);
        _eventSubWebsocketClient.ReconnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        var ex = new Exception("test");
        await _service.OnErrorOccurred(this, CreateErrorOccurredArgs(ex));
        await _twitchService.Received(1).ValidateAndRefreshToken();
        await _eventSubWebsocketClient.Received(1).ReconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Connect_WhenConnectSucceeds_Returns()
    {
        _eventSubWebsocketClient.ConnectAsync().Returns(true);
        await _service.Connect(CancellationToken.None);
        await _eventSubWebsocketClient.Received(1).ConnectAsync();
    }

    [Fact]
    public async Task Connect_WhenConnectFailsThenSucceeds_Returns()
    {
        _eventSubWebsocketClient.ConnectAsync().Returns(false, true);
        await _service.Connect(CancellationToken.None);
        await _eventSubWebsocketClient.Received(2).ConnectAsync();
    }

    [Fact]
    public async Task Connect_WhenCancelled_Returns()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _eventSubWebsocketClient.ConnectAsync().Returns(false);
        await _service.Connect(cts.Token);
    }

    private TwitchWebsocketHostedService CreateServiceWithTimeProvider(TimeProvider timeProvider)
    {
        return new TwitchWebsocketHostedService(
            _logger, _eventService, _eventSubWebsocketClient, _subscriptionHistory,
            _messageIdTracker, _memoryCache, _twitchService, timeProvider,
            _dispatcher, _twitchEventActionHandler, _chatColorService);
    }

    [Fact]
    public async Task OnWebsocketConnected_WhenNotRequestedReconnect_SubscribesAndSetsTimer()
    {
        _twitchService.SubscribeToAllTheStuffs("session-123").Returns(true);
        _eventSubWebsocketClient.SessionId.Returns("session-123");
        var mockTimeProvider = Substitute.For<TimeProvider>();
        var service = CreateServiceWithTimeProvider(mockTimeProvider);

        await service.OnWebsocketConnected(this, new WebsocketConnectedEventArgs
        {
            IsRequestedReconnect = false,
            KeepAliveTimeout = TimeSpan.FromSeconds(30)
        });

        await _twitchService.Received(1).SubscribeToAllTheStuffs("session-123");
        _logger.Received(1).LogInformation("Subscribed to events");
    }

    [Fact]
    public async Task OnWebsocketConnected_WhenSubscribeFails_Reconnects()
    {
        _twitchService.SubscribeToAllTheStuffs("session-123").Returns(false);
        _eventSubWebsocketClient.SessionId.Returns("session-123");
        var mockTimeProvider = Substitute.For<TimeProvider>();
        var service = CreateServiceWithTimeProvider(mockTimeProvider);

        _twitchService.ValidateAndRefreshToken().Returns(true);
        _eventSubWebsocketClient.ReconnectAsync(Arg.Any<CancellationToken>()).Returns(true);

        await service.OnWebsocketConnected(this, new WebsocketConnectedEventArgs
        {
            IsRequestedReconnect = false,
            KeepAliveTimeout = TimeSpan.FromSeconds(30)
        });

        await _twitchService.Received(1).SubscribeToAllTheStuffs("session-123");
        await _eventSubWebsocketClient.Received(1).ReconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnWebsocketConnected_WhenRequestedReconnect_DoesNotSubscribe()
    {
        _twitchService.SubscribeToAllTheStuffs("session-123").Returns(true);
        await _service.OnWebsocketConnected(this, new WebsocketConnectedEventArgs
        {
            IsRequestedReconnect = true,
            KeepAliveTimeout = TimeSpan.FromSeconds(30)
        });

        await _twitchService.DidNotReceive().SubscribeToAllTheStuffs(Arg.Any<string>());
    }

    [Fact]
    public async Task CheckWebsocketStatus_WhenKeepAliveTimerMinValue_ReturnsEarly()
    {
        typeof(TwitchWebsocketHostedService)
            .GetField("KeepAliveTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_service, TimeSpan.MinValue);

        _service.CheckWebsocketStatus(this);
        await Task.Yield();
        await _eventSubWebsocketClient.DidNotReceive().ReconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckWebsocketStatus_WhenNoRecentMessage_Reconnects()
    {
        typeof(TwitchWebsocketHostedService)
            .GetField("KeepAliveTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_service, TimeSpan.FromSeconds(30));
        typeof(TwitchWebsocketHostedService)
            .GetField("LastMessageReceived", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_service, DateTimeOffset.MinValue);

        _twitchService.IsServiceUp().Returns(true);
        _twitchService.ValidateAndRefreshToken().Returns(true);
        _eventSubWebsocketClient.ReconnectAsync(Arg.Any<CancellationToken>()).Returns(true);

        _service.CheckWebsocketStatus(this);
        await Task.Yield();
        await _eventSubWebsocketClient.Received(1).ReconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckWebsocketStatus_WhenRecentMessageReceived_DoesNotReconnect()
    {
        typeof(TwitchWebsocketHostedService)
            .GetField("KeepAliveTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_service, TimeSpan.FromSeconds(30));
        typeof(TwitchWebsocketHostedService)
            .GetField("LastMessageReceived", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_service, _timeProvider.GetLocalNow());

        _twitchService.IsServiceUp().Returns(true);

        _service.CheckWebsocketStatus(this);
        await Task.Yield();
        await _eventSubWebsocketClient.DidNotReceive().ReconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckWebsocketStatus_WhenServiceDown_DoesNotReconnect()
    {
        typeof(TwitchWebsocketHostedService)
            .GetField("KeepAliveTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_service, TimeSpan.FromSeconds(30));
        typeof(TwitchWebsocketHostedService)
            .GetField("LastMessageReceived", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_service, DateTimeOffset.MinValue);

        _twitchService.IsServiceUp().Returns(false);

        _service.CheckWebsocketStatus(this);
        await Task.Yield();
        await _eventSubWebsocketClient.DidNotReceive().ReconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WiresEventsAndCallsConnect()
    {
        var cts = new CancellationTokenSource();
        _twitchService.IsStreamOnline().Returns(false);
        _eventSubWebsocketClient.ConnectAsync().Returns(true);

        await _service.StartAsync(cts.Token);

        await _eventSubWebsocketClient.Received(1).ConnectAsync();
    }

    [Fact]
    public async Task StopAsync_UnwiresEventsAndDisconnects()
    {
        var cts = new CancellationTokenSource();
        await _service.StopAsync(cts.Token);

        await _eventSubWebsocketClient.Received(1).DisconnectAsync();
    }
}

#pragma warning restore NS1001, NS1004, NS5000

