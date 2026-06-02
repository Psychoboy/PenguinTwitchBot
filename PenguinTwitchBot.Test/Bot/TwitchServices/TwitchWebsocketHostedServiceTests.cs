using System.Reflection;
using TwitchLib.EventSub.Core.EventArgs;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.EventArgs.Stream;
using TwitchLib.EventSub.Core.Models;
using TwitchLib.EventSub.Core.Models.Bits;
using TwitchLib.EventSub.Core.Models.ChannelPoints;
using TwitchLib.EventSub.Core.Models.ChannelSuspiciousUser;
using TwitchLib.EventSub.Core.Models.Chat;
using TwitchLib.EventSub.Core.Models.Subscriptions;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Core.SubscriptionTypes.Stream;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events;
using PenguinTwitchBot.Bot.Services.Chat;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.TwitchApi.EventSub;
using EventSubChannel = PenguinTwitchBot.TwitchApi.EventSub.Channel;
using EventSubStream = PenguinTwitchBot.TwitchApi.EventSub.Stream;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TwitchLib.EventSub.Websockets;

namespace PenguinTwitchBot.Test.Bot.TwitchServices;

public class TwitchWebsocketHostedServiceTests
{
    private readonly IServiceBackbone _eventService;
    private readonly ITwitchService _twitchService;
    private readonly ITwitchEventActionHandler _twitchEventActionHandler;
    private readonly PenguinTwitchBot.Application.Notifications.IPenguinDispatcher _dispatcher;
    private readonly TwitchWebsocketHostedService _sut;

    public TwitchWebsocketHostedServiceTests()
    {
        _eventService = Substitute.For<IServiceBackbone>();
        _twitchService = Substitute.For<ITwitchService>();
        _twitchEventActionHandler = Substitute.For<ITwitchEventActionHandler>();
        _dispatcher = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var messageIdTracker = new ChatMessageIdTracker(memoryCache);
        var subscriptionTracker = new SubscriptionTracker(
            Substitute.For<ILogger<SubscriptionTracker>>(),
            Substitute.For<IServiceScopeFactory>());

        _sut = new TwitchWebsocketHostedService(
            Substitute.For<ILogger<TwitchWebsocketHostedService>>(),
            _eventService,
            new EventSubWebsocketClient(),
            subscriptionTracker,
            messageIdTracker,
            memoryCache,
            _twitchService,
            new FakeTimeProvider(),
            _dispatcher,
            _twitchEventActionHandler,
            Substitute.For<IChatColorService>());
    }

    [Fact]
    public async Task OnChannelRaidPayload_DispatchesRaidToBackboneAndActionHandler()
    {
        var payload = CreateRaidPayload("raid-msg-1", 42);

        await InvokePrivateAsync(_sut, "OnChannelRaid", payload);

        await _eventService.Received(1).OnIncomingRaid(Arg.Is<RaidEventArgs>(e =>
            e.UserId == "from-id" &&
            e.Name == "from-login" &&
            e.DisplayName == "FromName" &&
            e.NumberOfViewers == 42));

        await _twitchEventActionHandler.Received(1).HandleRaidAsync(Arg.Is<RaidEventArgs>(e =>
            e.UserId == "from-id" &&
            e.Name == "from-login" &&
            e.DisplayName == "FromName" &&
            e.NumberOfViewers == 42));
    }

    [Fact]
    public async Task OnChannelRaidPayload_DeduplicatesByMessageId()
    {
        var payload = CreateRaidPayload("raid-msg-dedupe", 12);

        await InvokePrivateAsync(_sut, "OnChannelRaid", payload);
        await InvokePrivateAsync(_sut, "OnChannelRaid", payload);

        await _eventService.Received(1).OnIncomingRaid(Arg.Any<RaidEventArgs>());
        await _twitchEventActionHandler.Received(1).HandleRaidAsync(Arg.Any<RaidEventArgs>());
    }

    [Fact]
    public async Task OnStreamOnlinePayload_SetsOnlineAndDispatches()
    {
        var payload = new EventSubStream.StreamOnlinePayload
        {
            Metadata = CreateMetadata("stream-online-1"),
            Event = new EventSubStream.StreamOnline()
        };

        await InvokePrivateAsync(_sut, "OnStreamOnline", payload);

        Assert.True(_eventService.IsOnline);
        await _eventService.Received(1).OnStreamStarted();
        await _twitchEventActionHandler.Received(1).HandleStreamOnlineAsync();
    }

    [Fact]
    public async Task OnStreamOfflinePayload_SetsOfflineAndDispatches()
    {
        _eventService.IsOnline = true;

        var payload = new EventSubStream.StreamOfflinePayload
        {
            Metadata = CreateMetadata("stream-offline-1"),
            Event = new EventSubStream.StreamOffline()
        };

        await InvokePrivateAsync(_sut, "OnStreamOffline", payload);

        Assert.False(_eventService.IsOnline);
        await _eventService.Received(1).OnStreamEnded();
        await _twitchEventActionHandler.Received(1).HandleStreamOfflineAsync();
    }

    [Fact]
    public async Task OnChannelFollow_ShouldDispatchFollow()
    {
        var args = CreateNotificationArgs<ChannelFollowArgs, ChannelFollow>("follow-1", new ChannelFollow
        {
            UserId = "u1",
            UserLogin = "login1",
            UserName = "User One",
            FollowedAt = DateTimeOffset.UtcNow,
        });

        await InvokeEventHandlerAsync(_sut, "OnChannelFollow", args);

        await _eventService.Received(1).OnFollow(Arg.Any<ChannelFollow>());
        await _twitchEventActionHandler.Received(1).HandleFollowAsync(Arg.Any<FollowEventArgs>());
    }

    [Fact]
    public async Task OnChannelCheer_ShouldDispatchCheer()
    {
        var args = CreateNotificationArgs<ChannelCheerArgs, ChannelCheer>("cheer-1", new ChannelCheer
        {
            UserId = "u2",
            UserLogin = "login2",
            UserName = "User Two",
            Bits = 100,
            Message = "cheer!"
        });

        await InvokeEventHandlerAsync(_sut, "OnChannelCheer", args);

        await _eventService.Received(1).OnCheer(Arg.Any<ChannelCheer>());
        await _twitchEventActionHandler.Received(1).HandleCheerAsync(Arg.Any<CheerEventArgs>());
    }

    [Fact]
    public async Task OnChannelSubscription_ShouldDispatchSubscribe()
    {
        var args = CreateNotificationArgs<ChannelSubscribeArgs, ChannelSubscribe>("sub-1", new ChannelSubscribe
        {
            UserId = "u3",
            UserLogin = "login3",
            UserName = "User Three",
            Tier = "1000",
            IsGift = false,
        }, subscriptionType: "channel.subscribe");

        await InvokeEventHandlerAsync(_sut, "OnChannelSubscription", args);

        await _eventService.Received(1).OnSubscription(Arg.Any<SubscriptionEventArgs>());
        await _twitchEventActionHandler.Received(1).HandleSubscribeAsync(Arg.Any<SubscriptionEventArgs>());
    }

    [Fact]
    public async Task OnChannelSubscriptionRenewal_ShouldDispatchRenewal()
    {
        var args = CreateNotificationArgs<ChannelSubscriptionMessageArgs, ChannelSubscriptionMessage>("renewal-1", new ChannelSubscriptionMessage
        {
            UserId = "u4",
            UserLogin = "login4",
            UserName = "User Four",
            Tier = "1000",
            CumulativeMonths = 3,
            StreakMonths = 2,
            Message = new SubscriptionMessage { Text = "Thanks!" },
        }, subscriptionType: "channel.subscription.message");

        await InvokeEventHandlerAsync(_sut, "OnChannelSubscriptionRenewal", args);

        await _eventService.Received(1).OnSubscription(Arg.Any<SubscriptionEventArgs>());
        await _twitchEventActionHandler.Received(1).HandleSubscribeAsync(Arg.Any<SubscriptionEventArgs>());
    }

    [Fact]
    public async Task OnChannelSubscriptionGift_ShouldDispatchGift()
    {
        var args = CreateNotificationArgs<ChannelSubscriptionGiftArgs, ChannelSubscriptionGift>("gift-1", new ChannelSubscriptionGift
        {
            UserId = "u5",
            UserLogin = "login5",
            UserName = "User Five",
            Total = 5,
            CumulativeTotal = 20,
        }, subscriptionType: "channel.subscription.gift");

        await InvokeEventHandlerAsync(_sut, "OnChannelSubscriptionGift", args);

        await _eventService.Received(1).OnSubscriptionGift(Arg.Any<SubscriptionGiftEventArgs>());
        await _twitchEventActionHandler.Received(1).HandleSubscriptionGiftAsync(Arg.Any<SubscriptionGiftEventArgs>());
    }

    [Fact]
    public async Task OnChannelSubscriptionEnd_ShouldDispatchEnd()
    {
        var args = CreateNotificationArgs<ChannelSubscriptionEndArgs, ChannelSubscriptionEnd>("sub-end-1", new ChannelSubscriptionEnd
        {
            UserId = "u6",
            UserLogin = "login6",
            UserName = "User Six",
        }, subscriptionType: "channel.subscription.end");

        await InvokeEventHandlerAsync(_sut, "OnChannelSubscriptionEnd", args);

        await _eventService.Received(1).OnSubscriptionEnd("login6", "u6");
        await _twitchEventActionHandler.Received(1).HandleSubscriptionEndAsync(Arg.Any<SubscriptionEndEventArgs>());
    }

    [Fact]
    public async Task OnChannelPointRedeemed_ShouldDispatchWhenNoUserInput()
    {
        var args = CreateNotificationArgs<ChannelPointsCustomRewardRedemptionArgs, ChannelPointsCustomRewardRedemption>("reward-1", new ChannelPointsCustomRewardRedemption
        {
            UserId = "u7",
            UserLogin = "login7",
            UserName = "User Seven",
            UserInput = string.Empty,
            Status = "unfulfilled",
            Reward = new RedemptionReward { Id = "r1", Title = "Reward" }
        });

        await InvokeEventHandlerAsync(_sut, "OnChannelPointRedeemed", args);

        await _eventService.Received(1).OnChannelPointRedeem("u7", "User Seven", "Reward");
        await _twitchEventActionHandler.Received(1).HandleChannelPointRedemptionAsync(Arg.Any<ChannelPointRedeemEventArgs>());
    }

    [Fact]
    public async Task OnChannelPointRedeemed_ShouldSkipWhenUserInputPresent()
    {
        var args = CreateNotificationArgs<ChannelPointsCustomRewardRedemptionArgs, ChannelPointsCustomRewardRedemption>("reward-2", new ChannelPointsCustomRewardRedemption
        {
            UserId = "u8",
            UserLogin = "login8",
            UserName = "User Eight",
            UserInput = "custom input",
            Status = "unfulfilled",
            Reward = new RedemptionReward { Id = "r2", Title = "Reward2" }
        });

        await InvokeEventHandlerAsync(_sut, "OnChannelPointRedeemed", args);

        await _eventService.DidNotReceive().OnChannelPointRedeem(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _twitchEventActionHandler.DidNotReceive().HandleChannelPointRedemptionAsync(Arg.Any<ChannelPointRedeemEventArgs>());
    }

    [Fact]
    public async Task OnChannelBan_ShouldDispatchPermanentBan()
    {
        var args = CreateNotificationArgs<ChannelBanArgs, ChannelBan>("ban-1", new ChannelBan
        {
            UserId = "u9",
            UserLogin = "login9",
            UserName = "User Nine",
            ModeratorUserId = "m1",
            ModeratorUserLogin = "mod",
            ModeratorUserName = "Mod",
            IsPermanent = true,
            Reason = "reason"
        });

        await InvokeEventHandlerAsync(_sut, "OnChannelBan", args);

        await _dispatcher.Received(1).Publish(Arg.Any<BannedChatUser>(), Arg.Any<CancellationToken>());
        await _eventService.Received(1).OnViewerBan("u9", "login9", false, Arg.Any<DateTimeOffset?>());
        await _twitchEventActionHandler.Received(1).HandleChannelBanAsync(Arg.Any<BanEventArgs>());
    }

    [Fact]
    public async Task OnChannelBan_ShouldDispatchTimeoutOnly()
    {
        var args = CreateNotificationArgs<ChannelBanArgs, ChannelBan>("ban-2", new ChannelBan
        {
            UserId = "u10",
            UserLogin = "login10",
            UserName = "User Ten",
            ModeratorUserId = "m2",
            ModeratorUserLogin = "mod2",
            ModeratorUserName = "Mod2",
            IsPermanent = false,
        });

        await InvokeEventHandlerAsync(_sut, "OnChannelBan", args);

        await _dispatcher.Received(1).Publish(Arg.Any<BannedChatUser>(), Arg.Any<CancellationToken>());
        await _eventService.DidNotReceive().OnViewerBan(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<DateTimeOffset?>());
    }

    [Fact]
    public async Task OnChannelUnBan_ShouldDispatchUnban()
    {
        var args = CreateNotificationArgs<ChannelUnbanArgs, ChannelUnban>("unban-1", new ChannelUnban
        {
            UserId = "u11",
            UserLogin = "login11",
            UserName = "User Eleven",
            ModeratorUserId = "m3",
            ModeratorUserLogin = "mod3",
            ModeratorUserName = "Mod3",
        });

        await InvokeEventHandlerAsync(_sut, "OnChannelUnBan", args);

        await _eventService.Received(1).OnViewerBan("u11", "login11", true, null);
        await _twitchEventActionHandler.Received(1).HandleChannelUnbanAsync(Arg.Any<BanEventArgs>());
    }

    [Fact]
    public async Task ChannelAdBreakBegin_ShouldDispatchAdBreak()
    {
        var args = CreateNotificationArgs<ChannelAdBreakBeginArgs, ChannelAdBreakBegin>("ad-1", new ChannelAdBreakBegin
        {
            DurationSeconds = 60,
            IsAutomatic = false,
            StartedAt = DateTimeOffset.UtcNow,
        });

        await InvokeEventHandlerAsync(_sut, "ChannelAdBreakBegin", args);

        await _eventService.Received(1).OnAdBreakStartEvent(Arg.Any<AdBreakStartEventArgs>());
        await _twitchEventActionHandler.Received(1).HandleAdBreakBeginAsync(Arg.Any<AdBreakStartEventArgs>());
    }

    [Fact]
    public async Task OnChannelBitsUse_ShouldDispatchBitsActionHandler()
    {
        var args = CreateNotificationArgs<ChannelBitsUseArgs, ChannelBitsUse>("bits-1", new ChannelBitsUse
        {
            UserId = "u12",
            UserLogin = "login12",
            UserName = "User Twelve",
            Bits = 250,
            Type = "cheer",
            Message = new TwitchLib.EventSub.Core.Models.Bits.BitsMessage { Text = "bits" }
        });

        await InvokeEventHandlerAsync(_sut, "OnChannelBitsUse", args);

        await _twitchEventActionHandler.Received(1).HandleBitsUseAsync(Arg.Any<BitsUseEventArgs>());
    }

    [Fact]
    public async Task ChannelChatMessageDelete_ShouldPublishDeletedChatNotification()
    {
        var args = CreateNotificationArgs<ChannelChatMessageDeleteArgs, ChannelChatMessageDelete>("delete-1", new ChannelChatMessageDelete
        {
            MessageId = "msg-delete",
            TargetUserId = "u13",
            TargetUserLogin = "login13",
            TargetUserName = "User Thirteen",
        });

        await InvokeEventHandlerAsync(_sut, "ChannelChatMessageDelete", args);

        await _dispatcher.Received(1).Publish(Arg.Is<DeletedChatMessage>(n => n.EventArgs == args), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChannelSuspiciousUserMessage_ShouldPublishReceivedChatMessage()
    {
        var args = CreateNotificationArgs<ChannelSuspiciousUserMessageArgs, ChannelSuspiciousUserMessage>("suspicious-1", new ChannelSuspiciousUserMessage
        {
            UserId = "u14",
            UserLogin = "login14",
            UserName = "User Fourteen",
            Message = new SuspiciousUserMessage { MessageId = "sus-msg", Text = "hello" }
        });

        await InvokeEventHandlerAsync(_sut, "ChannelSuspiciousUserMessage", args);

        await _dispatcher.Received(1).Publish(Arg.Any<ReceivedChatMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnChannelChatNotification_ShouldDispatchChatNotificationAction()
    {
        var args = CreateNotificationArgs<ChannelChatNotificationArgs, ChannelChatNotification>("chat-notice-1", new ChannelChatNotification
        {
            MessageId = "notice-msg",
            ChatterUserId = "u15",
            ChatterUserLogin = "login15",
            ChatterUserName = "User Fifteen",
            NoticeType = "announcement",
            Message = new ChatMessage { Text = "notice" },
        });

        await InvokeEventHandlerAsync(_sut, "OnChannelChatNotification", args);

        await _twitchEventActionHandler.Received(1).HandleChatNotificationAsync(Arg.Any<ChatNotificationEventArgs>());
    }

    [Fact]
    public async Task ChannelChatMessage_ShouldPublishChatMessageDispatch()
    {
        var args = CreateNotificationArgs<ChannelChatMessageArgs, ChannelChatMessage>("chat-msg-1", new ChannelChatMessage
        {
            MessageId = "chat-msg-1",
            ChatterUserId = "u16",
            ChatterUserLogin = "login16",
            ChatterUserName = "User Sixteen",
            Message = new ChatMessage { Text = "hello chat" },
            Color = "#ffffff",
            Badges = Array.Empty<ChatBadge>(),
        });

        await InvokeEventHandlerAsync(_sut, "ChannelChatMessage", args);

        await _dispatcher.Received(1).Publish(Arg.Any<ReceivedChatMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MessageReceived_ShouldUpdateLastMessageTimestamp()
    {
        var field = typeof(TwitchWebsocketHostedService).GetField("LastMessageReceived", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var before = (DateTimeOffset)field!.GetValue(_sut)!;

        await InvokeEventHandlerAsync(_sut, "MessageReceived", new MessageReceivedEventArgs());

        var after = (DateTimeOffset)field.GetValue(_sut)!;
        Assert.True(after > before);
    }

    [Fact]
    public async Task OnWebsocketConnected_WhenRequestedReconnect_ShouldNoOp()
    {
        await InvokeEventHandlerAsync(_sut, "OnWebsocketConnected", new WebsocketConnectedArgs
        {
            IsRequestedReconnect = true,
            KeepAliveTimeout = TimeSpan.FromSeconds(10)
        });

        await _twitchService.DidNotReceive().SubscribeToAllTheStuffs(Arg.Any<string>());
    }

    [Fact]
    public async Task OnWebsocketDisconnected_WhenAlreadyReconnecting_ShouldReturnQuickly()
    {
        SetPrivateField(_sut, "Reconnecting", true);
        await InvokeEventHandlerAsync(_sut, "OnWebsocketDisconnected", EventArgs.Empty);
    }

    [Fact]
    public async Task OnWebsocketReconnected_ShouldReturnCompletedTask()
    {
        await InvokeEventHandlerAsync(_sut, "OnWebsocketReconnected", EventArgs.Empty);
    }

    [Fact]
    public async Task OnErrorOccurred_WhenAlreadyReconnecting_ShouldReturnQuickly()
    {
        SetPrivateField(_sut, "Reconnecting", true);
        await InvokeEventHandlerAsync(_sut, "OnErrorOccurred", new ErrorOccuredArgs());
    }

    private static EventSubChannel.ChannelRaidPayload CreateRaidPayload(string messageId, int viewers)
    {
        return new EventSubChannel.ChannelRaidPayload
        {
            Metadata = CreateMetadata(messageId),
            Event = new EventSubChannel.ChannelRaid
            {
                FromBroadcasterUserId = "from-id",
                FromBroadcasterUserLogin = "from-login",
                FromBroadcasterUserName = "FromName",
                ToBroadcasterUserId = "to-id",
                ToBroadcasterUserLogin = "to-login",
                ToBroadcasterUserName = "ToName",
                Viewers = viewers
            }
        };
    }

    private static PenguinTwitchBot.TwitchApi.EventSub.EventSubMetadata CreateMetadata(string messageId)
    {
        return new PenguinTwitchBot.TwitchApi.EventSub.EventSubMetadata
        {
            MessageId = messageId,
            MessageType = "notification",
            MessageTimestamp = DateTime.UtcNow
        };
    }

    private static async Task InvokePrivateAsync<TArg>(object target, string methodName, TArg arg)
    {
        var method = target.GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(m => m.Name == methodName
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(TArg));

        var task = (Task?)method.Invoke(target, [arg]);
        if (task is null)
        {
            throw new InvalidOperationException($"Private method {methodName} did not return Task.");
        }

        await task;
    }

    private static async Task InvokeEventHandlerAsync<TArgs>(object target, string methodName, TArgs args)
    {
        var method = target.GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(m => m.Name == methodName
                && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType == typeof(TArgs));

        var task = (Task?)method.Invoke(target, [null, args]);
        if (task is null)
        {
            throw new InvalidOperationException($"Private event handler {methodName} did not return Task.");
        }

        await task;
    }

    private static TArgs CreateNotificationArgs<TArgs, TEvent>(string messageId, TEvent eventData, string subscriptionType = "notification")
        where TArgs : TwitchLibEventSubNotificationArgs<TEvent>, new()
    {
        return new TArgs
        {
            Metadata = new TwitchLib.EventSub.Websockets.Core.Models.WebsocketEventSubMetadata
            {
                MessageId = messageId,
                MessageType = "notification",
                MessageTimestamp = DateTime.UtcNow,
                SubscriptionType = subscriptionType,
                SubscriptionVersion = "1"
            },
            Payload = new EventSubNotificationPayload<TEvent>
            {
                Event = eventData,
                Subscription = new EventSubSubscription
                {
                    Type = subscriptionType,
                    Version = "1",
                    Condition = new Dictionary<string, string>(),
                    Transport = new EventSubTransport { Method = "websocket" },
                    Status = "enabled",
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            }
        };
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field is null)
        {
            throw new InvalidOperationException($"Could not find field '{fieldName}'.");
        }

        field.SetValue(target, value);
    }

}
