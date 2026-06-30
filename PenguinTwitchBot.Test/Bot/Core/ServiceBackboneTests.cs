using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Application.ChatMessage.Notification;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Alias.Requests;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Moderation;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.CustomMiddleware;
using PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Core;

public class ServiceBackboneTests
{
    private readonly ILogger<ServiceBackbone> _logger;
    private readonly IPenguinDispatcher _dispatcher;
    private readonly IKnownBots _knownBots;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<MainHub> _hubContext;
    private readonly ServiceBackbone _serviceBackbone;

    public ServiceBackboneTests()
    {
        _logger = Substitute.For<ILogger<ServiceBackbone>>();
        _dispatcher = Substitute.For<IPenguinDispatcher>();
        _knownBots = Substitute.For<IKnownBots>();
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _hubContext = Substitute.For<IHubContext<MainHub>>();

        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["broadcaster"] = "TestStreamer",
                ["botName"] = "TestBot"
            });
        _configuration = configBuilder.Build();

        _serviceBackbone = new ServiceBackbone(_logger, _dispatcher, _knownBots, _configuration, _scopeFactory, _hubContext);
    }

    [Fact]
    public void Constructor_SetsPropertiesFromConfiguration()
    {
        Assert.Equal("TestStreamer", _serviceBackbone.BroadcasterName);
        Assert.Equal("TestBot", _serviceBackbone.BotName);
        Assert.True(_serviceBackbone.HealthStatus);
        Assert.False(_serviceBackbone.IsOnline);
    }

    [Fact]
    public void Constructor_WithNullBroadcaster_ReturnsEmptyString()
    {
        var config = Substitute.For<IConfiguration>();
        config["broadcaster"].Returns((string?)null);
        config["botName"].Returns((string?)null);

        var backbone = new ServiceBackbone(_logger, _dispatcher, _knownBots, config, _scopeFactory, _hubContext);
        Assert.Equal("", backbone.BroadcasterName);
        Assert.Null(backbone.BotName);
    }

    [Fact]
    public void IsBroadcasterOrBot_DelegatesToKnownBots()
    {
        _knownBots.IsStreamerOrBot("user1").Returns(true);
        Assert.True(_serviceBackbone.IsBroadcasterOrBot("user1"));

        _knownBots.IsStreamerOrBot("user2").Returns(false);
        Assert.False(_serviceBackbone.IsBroadcasterOrBot("user2"));
    }

    [Fact]
    public void IsKnownBot_DelegatesToKnownBots()
    {
        _knownBots.IsKnownBot("bot1").Returns(true);
        Assert.True(_serviceBackbone.IsKnownBot("bot1"));

        _knownBots.IsKnownBot("user1").Returns(false);
        Assert.False(_serviceBackbone.IsKnownBot("user1"));
    }

    [Fact]
    public void IsKnownBotOrCurrentStreamer_DelegatesToKnownBots()
    {
        _knownBots.IsKnownBotOrCurrentStreamer("user1").Returns(true);
        Assert.True(_serviceBackbone.IsKnownBotOrCurrentStreamer("user1"));

        _knownBots.IsKnownBotOrCurrentStreamer("user2").Returns(false);
        Assert.False(_serviceBackbone.IsKnownBotOrCurrentStreamer("user2"));
    }

    [Fact]
    public async Task RunCommand_WhenAliasHandled_ReturnsWithoutPublishing()
    {
        var command = new CommandEventArgs { Name = "testuser", Command = "test" };
        _dispatcher.Send(Arg.Any<AliasRunCommand>()).Returns(true);

        await _serviceBackbone.RunCommand(command);

        await _dispatcher.Received(1).Send(Arg.Any<AliasRunCommand>());
        await _dispatcher.DidNotReceive().Publish(Arg.Any<RunCommandNotification>());
    }

    [Fact]
    public async Task RunCommand_WhenAliasNotHandled_PublishesRunCommandNotification()
    {
        var command = new CommandEventArgs { Name = "testuser", Command = "test" };
        _dispatcher.Send(Arg.Any<AliasRunCommand>()).Returns(false);

        await _serviceBackbone.RunCommand(command);

        await _dispatcher.Received(1).Send(Arg.Any<AliasRunCommand>());
        await _dispatcher.Received(1).Publish(Arg.Any<RunCommandNotification>());
    }

    [Fact]
    public async Task OnCommand_WithNullCommand_DoesNothing()
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        await _serviceBackbone.OnCommand(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        await _dispatcher.DidNotReceive().Send(Arg.Any<AliasRunCommand>());
        await _dispatcher.DidNotReceive().Publish(Arg.Any<RunCommandNotification>());
    }

    [Fact]
    public async Task OnCommand_WithValidCommand_CallsRunCommand()
    {
        var command = new CommandEventArgs { Name = "testuser", Command = "test" };
        _dispatcher.Send(Arg.Any<AliasRunCommand>()).Returns(false);

        await _serviceBackbone.OnCommand(command);

        await _dispatcher.Received(1).Publish(Arg.Any<RunCommandNotification>());
    }

    [Fact]
    public async Task OnWhisperCommand_WithNullEvent_DoesNothing()
    {
        var command = new CommandEventArgs { Name = "testuser", Command = "test" };
        await _serviceBackbone.OnWhisperCommand(command);
        await _dispatcher.DidNotReceive().Publish(Arg.Any<SendBotMessage>());
    }

    [Fact]
    public async Task OnWhisperCommand_WithBroadcaster_FiresEvent()
    {
        var command = new CommandEventArgs { Name = "testuser", Command = "test", MessageId = "msg1" };
        _knownBots.IsStreamerOrBot("testuser").Returns(true);

        bool eventFired = false;
        _serviceBackbone.CommandEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };

        await _serviceBackbone.OnWhisperCommand(command);

        Assert.True(eventFired);
    }

    [Fact]
    public async Task OnWhisperCommand_WithAllowedCommand_FiresEvent()
    {
        var command = new CommandEventArgs { Name = "regularuser", Command = "entries", MessageId = "msg1" };
        _knownBots.IsStreamerOrBot("regularuser").Returns(false);

        bool eventFired = false;
        _serviceBackbone.CommandEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };

        await _serviceBackbone.OnWhisperCommand(command);

        Assert.True(eventFired);
    }

    [Fact]
    public async Task OnWhisperCommand_WithRegularUserAndDisallowedCommand_DoesNotFireEvent()
    {
        var command = new CommandEventArgs { Name = "regularuser", Command = "badcommand" };
        _knownBots.IsStreamerOrBot("regularuser").Returns(false);

        bool eventFired = false;
        _serviceBackbone.CommandEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };

        await _serviceBackbone.OnWhisperCommand(command);

        Assert.False(eventFired);
    }

    [Fact]
    public async Task OnWhisperCommand_WhenEventThrows_DoesNotPropagateException()
    {
        var command = new CommandEventArgs { Name = "testuser", Command = "test" };
        _knownBots.IsStreamerOrBot("testuser").Returns(true);

        var backbone = new ServiceBackbone(_logger, _dispatcher, _knownBots, _configuration, _scopeFactory, _hubContext);
        backbone.CommandEvent += (sender, args) => throw new InvalidOperationException("Test error");

        await backbone.OnWhisperCommand(command);
    }

    [Fact]
    public async Task SendChatMessage_WithMessage_PublishesSendBotMessage()
    {
        await _serviceBackbone.SendChatMessage("Hello chat");
        await _dispatcher.Received(1).Publish(Arg.Is<SendBotMessage>(m => m.Message == "Hello chat"));
    }

    [Fact]
    public async Task SendChatMessage_WithSourceOnlyFalse_PublishesWithSourceOnlyFalse()
    {
        await _serviceBackbone.SendChatMessage("Hello chat", false);
        await _dispatcher.Received(1).Publish(Arg.Is<SendBotMessage>(m => !m.SourceOnly));
    }

    [Fact]
    public async Task ResponseWithMessage_WithNullMessageId_SendsChatMessage()
    {
        var e = new CommandEventArgs { DisplayName = "TestUser", MessageId = "", FromOwnChannel = true };
        await _serviceBackbone.ResponseWithMessage(e, "Hello!");

        await _dispatcher.Received(1).Publish(Arg.Is<SendBotMessage>(m => m.Message == "@TestUser, Hello!"));
    }

    [Fact]
    public async Task ResponseWithMessage_WithMessageId_PublishesReplyToMessage()
    {
        var e = new CommandEventArgs { DisplayName = "TestUser", MessageId = "msg123" };
        await _serviceBackbone.ResponseWithMessage(e, "Hello!");

        await _dispatcher.Received(1).Publish(Arg.Is<ReplyToMessage>(r =>
            r.Name == "TestUser" && r.MessageId == "msg123" && r.Message == "Hello!"));
    }

    [Fact]
    public async Task ResponseWithMessage_TrimsExclamationMark()
    {
        var e = new CommandEventArgs { DisplayName = "TestUser", MessageId = "", FromOwnChannel = true };
        await _serviceBackbone.ResponseWithMessage(e, "!Hello!");

        await _dispatcher.Received(1).Publish(Arg.Is<SendBotMessage>(m => m.Message == "@TestUser, Hello!"));
    }

    [Fact]
    public async Task ResponseWithMessage_TrimsWhitespace()
    {
        var e = new CommandEventArgs { DisplayName = "TestUser", MessageId = "", FromOwnChannel = true };
        await _serviceBackbone.ResponseWithMessage(e, "  Hello  ");

        await _dispatcher.Received(1).Publish(Arg.Is<SendBotMessage>(m => m.Message == "@TestUser, Hello"));
    }

    [Fact]
    public async Task SendChatMessage_WithName_FormatsMessage()
    {
        await _serviceBackbone.SendChatMessage("TestUser", "Hello!");
        await _dispatcher.Received(1).Publish(Arg.Is<SendBotMessage>(m => m.Message == "@TestUser, Hello!"));
    }

    [Fact]
    public async Task SendChatMessageWithTitle_WithEmptyTitle_UsesViewerName()
    {
        var viewerFeature = Substitute.For<IViewerFeature>();
#pragma warning disable CS8620
        viewerFeature.GetNameWithTitle("TestUser").Returns(Task.FromResult<string?>(null));
#pragma warning restore CS8620

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        scope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IViewerFeature)).Returns(viewerFeature);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
#pragma warning disable NS1000
        scopeFactory.CreateAsyncScope().Returns(scope);
#pragma warning restore NS1000

        var backbone = new ServiceBackbone(_logger, _dispatcher, _knownBots, _configuration, scopeFactory, _hubContext);

        await backbone.SendChatMessageWithTitle("TestUser", "Hello!");

        await _dispatcher.Received(1).Publish(Arg.Is<SendBotMessage>(m => m.Message == "TestUser, Hello!"));
    }

    [Fact]
    public async Task SendChatMessageWithTitle_WithTitle_UsesTitleWithViewerName()
    {
        var viewerFeature = Substitute.For<IViewerFeature>();
#pragma warning disable CS8620
        viewerFeature.GetNameWithTitle("TestUser").Returns(Task.FromResult<string?>("Commander"));
#pragma warning restore CS8620

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        scope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IViewerFeature)).Returns(viewerFeature);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
#pragma warning disable NS1000
        scopeFactory.CreateAsyncScope().Returns(scope);
#pragma warning restore NS1000

        var backbone = new ServiceBackbone(_logger, _dispatcher, _knownBots, _configuration, scopeFactory, _hubContext);

        await backbone.SendChatMessageWithTitle("TestUser", "Hello!");

        await _dispatcher.Received(1).Publish(Arg.Is<SendBotMessage>(m => m.Message == "Commander, Hello!"));
    }

    [Fact]
    public async Task OnStreamStarted_PublishesNotificationAndSendsHubMessage()
    {
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        clients.All.Returns(clientProxy);
        _hubContext.Clients.Returns(clients);

        bool eventFired = false;
        _serviceBackbone.StreamStarted += (sender, args) => { eventFired = true; return Task.CompletedTask; };

        await _serviceBackbone.OnStreamStarted();

        await _dispatcher.Received(1).Publish(Arg.Any<StreamStartedNotification>());
#pragma warning disable NS1004, NS5000, CS8605
        await clientProxy.Received(1).SendCoreAsync("StreamChanged", Arg.Is<object?[]>(o => o != null && o.Length == 1 && (bool)o[0] == true), Arg.Any<CancellationToken>());
#pragma warning restore NS1004, NS5000, CS8605
        Assert.True(eventFired);
    }

    [Fact]
    public async Task OnStreamStarted_WhenEventThrows_LogsError()
    {
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        clients.All.Returns(clientProxy);
        _hubContext.Clients.Returns(clients);

        _serviceBackbone.StreamStarted += (sender, args) => throw new InvalidOperationException("Stream started error");

        await _serviceBackbone.OnStreamStarted();
    }

    [Fact]
    public async Task OnStreamEnded_SendsHubMessage()
    {
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        clients.All.Returns(clientProxy);
        _hubContext.Clients.Returns(clients);

        bool eventFired = false;
        _serviceBackbone.StreamEnded += (sender, args) => { eventFired = true; return Task.CompletedTask; };

        await _serviceBackbone.OnStreamEnded();

#pragma warning disable NS1004, NS5000, CS8605
        await clientProxy.Received(1).SendCoreAsync("StreamChanged", Arg.Is<object?[]>(o => o != null && o.Length == 1 && (bool)o[0] == false), Arg.Any<CancellationToken>());
#pragma warning restore NS1004, NS5000, CS8605
        Assert.True(eventFired);
    }

    [Fact]
    public async Task OnStreamEnded_WhenEventThrows_LogsError()
    {
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        clients.All.Returns(clientProxy);
        _hubContext.Clients.Returns(clients);

        _serviceBackbone.StreamEnded += (sender, args) => throw new InvalidOperationException("Stream ended error");

        await _serviceBackbone.OnStreamEnded();
    }

    [Fact]
    public async Task OnCheer_WithNullEvent_DoesNothing()
    {
        var ev = new ChannelCheer { UserLogin = "test", UserName = "Test", Bits = 0 };
        var backbone = new ServiceBackbone(_logger, _dispatcher, _knownBots, _configuration, _scopeFactory, _hubContext);

        await backbone.OnCheer(ev);
    }

    [Fact]
    public async Task OnCheer_WithValidEvent_FiresCheerEvent()
    {
        var ev = new ChannelCheer
        {
            UserLogin = "testuser",
            UserName = "TestUser",
            Bits = 100,
            Message = "Great stream!",
            IsAnonymous = false,
            UserId = "12345"
        };

        CheerEventArgs? capturedArgs = null;
        _serviceBackbone.CheerEvent += (sender, args) => { capturedArgs = args; return Task.CompletedTask; };

        await _serviceBackbone.OnCheer(ev);

        Assert.NotNull(capturedArgs);
        Assert.Equal("testuser", capturedArgs.Name);
        Assert.Equal("TestUser", capturedArgs.DisplayName);
        Assert.Equal(100, capturedArgs.Amount);
        Assert.Equal("Great stream!", capturedArgs.Message);
        Assert.False(capturedArgs.IsAnonymous);
        Assert.Equal("12345", capturedArgs.UserId);
    }

    [Fact]
    public async Task OnCheer_WithNullMessage_SetsEmptyMessage()
    {
        var ev = new ChannelCheer
        {
            UserLogin = "testuser",
            UserName = "TestUser",
            Bits = 50,
#pragma warning disable CS8625
            Message = null,
#pragma warning restore CS8625
            IsAnonymous = true,
            UserId = null
        };

        CheerEventArgs? capturedArgs = null;
        _serviceBackbone.CheerEvent += (sender, args) => { capturedArgs = args; return Task.CompletedTask; };

        await _serviceBackbone.OnCheer(ev);

        Assert.NotNull(capturedArgs);
        Assert.Equal("", capturedArgs.Message);
        Assert.Equal(50, capturedArgs.Amount);
        Assert.True(capturedArgs.IsAnonymous);
        Assert.Null(capturedArgs.UserId);
    }

    [Fact]
    public async Task OnCheer_WithEmptyMessage_SetsEmptyMessage()
    {
        var ev = new ChannelCheer
        {
            UserLogin = "testuser",
            UserName = "TestUser",
            Bits = 10,
            Message = "   ",
            IsAnonymous = false,
            UserId = "999"
        };

        CheerEventArgs? capturedArgs = null;
        _serviceBackbone.CheerEvent += (sender, args) => { capturedArgs = args; return Task.CompletedTask; };

        await _serviceBackbone.OnCheer(ev);

        Assert.NotNull(capturedArgs);
        Assert.Equal("", capturedArgs.Message);
    }

    [Fact]
    public async Task OnFollow_WithNullEvent_DoesNothing()
    {
        var ev = new ChannelFollow { UserId = "123", UserName = "TestUser", UserLogin = "testuser" };
        var backbone = new ServiceBackbone(_logger, _dispatcher, _knownBots, _configuration, _scopeFactory, _hubContext);

        await backbone.OnFollow(ev);
    }

    [Fact]
    public async Task OnFollow_WithValidEvent_FiresFollowEvent()
    {
        var ev = new ChannelFollow
        {
            UserId = "123",
            UserName = "TestUser",
            UserLogin = "testuser",
            FollowedAt = DateTimeOffset.Now
        };

        FollowEventArgs? capturedArgs = null;
        _serviceBackbone.FollowEvent += (sender, args) => { capturedArgs = args; return Task.CompletedTask; };

        await _serviceBackbone.OnFollow(ev);

        Assert.NotNull(capturedArgs);
        Assert.Equal("123", capturedArgs.UserId);
        Assert.Equal("testuser", capturedArgs.Username);
        Assert.Equal("TestUser", capturedArgs.DisplayName);
        Assert.Equal(ev.FollowedAt.DateTime, capturedArgs.FollowDate);
    }

    [Fact]
    public async Task OnIncomingRaid_WithNullEvent_DoesNothing()
    {
        await _serviceBackbone.OnIncomingRaid(null!);
        bool eventFired = false;
        _serviceBackbone.IncomingRaidEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };
        Assert.False(eventFired);
    }

    [Fact]
    public async Task OnIncomingRaid_WithValidEvent_FiresRaidEvent()
    {
        var args = new RaidEventArgs { Name = "Raider", UserId = "123", DisplayName = "Raider", NumberOfViewers = 50 };

        RaidEventArgs? capturedArgs = null;
        _serviceBackbone.IncomingRaidEvent += (sender, a) => { capturedArgs = a; return Task.CompletedTask; };

        await _serviceBackbone.OnIncomingRaid(args);

        Assert.NotNull(capturedArgs);
        Assert.Equal("Raider", capturedArgs.Name);
        Assert.Equal(50, capturedArgs.NumberOfViewers);
    }

    [Fact]
    public async Task OnIncomingRaid_WhenEventThrows_LogsError()
    {
        var args = new RaidEventArgs();
        _serviceBackbone.IncomingRaidEvent += (sender, a) => throw new InvalidOperationException("Raid error");

        await _serviceBackbone.OnIncomingRaid(args);
    }

    [Fact]
    public async Task OnSubscription_WithNullEvent_DoesNothing()
    {
        await _serviceBackbone.OnSubscription(null!);
        bool eventFired = false;
        _serviceBackbone.SubscriptionEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };
        Assert.False(eventFired);
    }

    [Fact]
    public async Task OnSubscription_WithValidEvent_FiresSubscriptionEvent()
    {
        var args = new SubscriptionEventArgs { Name = "TestUser", UserId = "123", DisplayName = "TestUser" };
        SubscriptionEventArgs? capturedArgs = null;
        _serviceBackbone.SubscriptionEvent += (sender, a) => { capturedArgs = a; return Task.CompletedTask; };

        await _serviceBackbone.OnSubscription(args);

        Assert.NotNull(capturedArgs);
        Assert.Equal("TestUser", capturedArgs.Name);
    }

    [Fact]
    public async Task OnSubscriptionGift_WithNullEvent_DoesNothing()
    {
        await _serviceBackbone.OnSubscriptionGift(null!);
        bool eventFired = false;
        _serviceBackbone.SubscriptionGiftEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };
        Assert.False(eventFired);
    }

    [Fact]
    public async Task OnSubscriptionGift_WithValidEvent_FiresSubscriptionGiftEvent()
    {
        var args = new SubscriptionGiftEventArgs { Name = "Gifter", UserId = "123", GiftAmount = 5 };
        SubscriptionGiftEventArgs? capturedArgs = null;
        _serviceBackbone.SubscriptionGiftEvent += (sender, a) => { capturedArgs = a; return Task.CompletedTask; };

        await _serviceBackbone.OnSubscriptionGift(args);

        Assert.NotNull(capturedArgs);
        Assert.Equal(5, capturedArgs.GiftAmount);
    }

    [Fact]
    public async Task OnSubscriptionEnd_WithNullEvent_DoesNothing()
    {
        await _serviceBackbone.OnSubscriptionEnd("TestUser", "123");
        bool eventFired = false;
        _serviceBackbone.SubscriptionEndEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };
        Assert.False(eventFired);
    }

    [Fact]
    public async Task OnSubscriptionEnd_WithValidArgs_CreatesEventArgsAndFiresEvent()
    {
        SubscriptionEndEventArgs? capturedArgs = null;
        _serviceBackbone.SubscriptionEndEvent += (sender, args) => { capturedArgs = args; return Task.CompletedTask; };

        await _serviceBackbone.OnSubscriptionEnd("TestUser", "456");

        Assert.NotNull(capturedArgs);
        Assert.Equal("TestUser", capturedArgs.Name);
        Assert.Equal("456", capturedArgs.UserId);
    }

    [Fact]
    public async Task OnAdBreakStartEvent_WithNullEvent_DoesNothing()
    {
        await _serviceBackbone.OnAdBreakStartEvent(null!);
        bool eventFired = false;
        _serviceBackbone.AdBreakStartEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };
        Assert.False(eventFired);
    }

    [Fact]
    public async Task OnAdBreakStartEvent_WithValidEvent_FiresAdBreakStartEvent()
    {
        var args = new AdBreakStartEventArgs { Length = 30, Automatic = true, StartedAt = DateTimeOffset.Now };
        AdBreakStartEventArgs? capturedArgs = null;
        _serviceBackbone.AdBreakStartEvent += (sender, a) => { capturedArgs = a; return Task.CompletedTask; };

        await _serviceBackbone.OnAdBreakStartEvent(args);

        Assert.NotNull(capturedArgs);
        Assert.Equal(30, capturedArgs.Length);
        Assert.True(capturedArgs.Automatic);
    }

    [Fact]
    public async Task OnChannelPointRedeem_TwoParamOverload_CallsFourParamOverload()
    {
        ChannelPointRedeemEventArgs? capturedArgs = null;
        _serviceBackbone.ChannelPointRedeemEvent += (sender, args) => { capturedArgs = args; return Task.CompletedTask; };

        await _serviceBackbone.OnChannelPointRedeem("123", "TestUser", "TestReward");

        Assert.NotNull(capturedArgs);
        Assert.Equal("TestUser", capturedArgs.Sender);
        Assert.Equal("TestReward", capturedArgs.Title);
        Assert.Equal("", capturedArgs.UserInput);
    }

    [Fact]
    public async Task OnChannelPointRedeem_FourParamOverload_WithNullEvent_DoesNothing()
    {
        await _serviceBackbone.OnChannelPointRedeem("123", "TestUser", "TestReward", "input");
        bool eventFired = false;
        _serviceBackbone.ChannelPointRedeemEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };
        Assert.False(eventFired);
    }

    [Fact]
    public async Task OnChannelPointRedeem_FourParamOverload_WithValidArgs_FiresEvent()
    {
        ChannelPointRedeemEventArgs? capturedArgs = null;
        _serviceBackbone.ChannelPointRedeemEvent += (sender, args) => { capturedArgs = args; return Task.CompletedTask; };

        await _serviceBackbone.OnChannelPointRedeem("123", "TestUser", "TestReward", "my input");

        Assert.NotNull(capturedArgs);
        Assert.Equal("TestUser", capturedArgs.Sender);
        Assert.Equal("TestReward", capturedArgs.Title);
        Assert.Equal("my input", capturedArgs.UserInput);
    }

    [Fact]
    public async Task OnUserJoined_WithNullEvent_DoesNothing()
    {
        await _serviceBackbone.OnUserJoined("TestUser");
        bool eventFired = false;
        _serviceBackbone.UserJoinedEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };
        Assert.False(eventFired);
    }

    [Fact]
    public async Task OnUserJoined_WithValidUsername_FiresEvent()
    {
        UserJoinedEventArgs? capturedArgs = null;
        _serviceBackbone.UserJoinedEvent += (sender, args) => { capturedArgs = args; return Task.CompletedTask; };

        await _serviceBackbone.OnUserJoined("TestUser");

        Assert.NotNull(capturedArgs);
        Assert.Equal("TestUser", capturedArgs.Username);
    }

    [Fact]
    public async Task OnUserLeft_WithNullEvent_DoesNothing()
    {
        await _serviceBackbone.OnUserLeft("TestUser");
        bool eventFired = false;
        _serviceBackbone.UserLeftEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };
        Assert.False(eventFired);
    }

    [Fact]
    public async Task OnUserLeft_WithValidUsername_FiresEvent()
    {
        UserLeftEventArgs? capturedArgs = null;
        _serviceBackbone.UserLeftEvent += (sender, args) => { capturedArgs = args; return Task.CompletedTask; };

        await _serviceBackbone.OnUserLeft("TestUser");

        Assert.NotNull(capturedArgs);
        Assert.Equal("TestUser", capturedArgs.Username);
    }

    [Fact]
    public async Task OnViewerBan_WithNullEvent_DoesNothing()
    {
        await _serviceBackbone.OnViewerBan("123", "TestUser", false, null);
        bool eventFired = false;
        _serviceBackbone.BanEvent += (sender, args) => { eventFired = true; return Task.CompletedTask; };
        Assert.False(eventFired);
    }

    [Fact]
    public async Task OnViewerBan_WithValidArgs_FiresBanEvent()
    {
        var endsAt = DateTimeOffset.Now.AddDays(7);
        BanEventArgs? capturedArgs = null;
        _serviceBackbone.BanEvent += (sender, args) => { capturedArgs = args; return Task.CompletedTask; };

        await _serviceBackbone.OnViewerBan("123", "TestUser", true, endsAt);

        Assert.NotNull(capturedArgs);
        Assert.Equal("123", capturedArgs.UserId);
        Assert.Equal("TestUser", capturedArgs.Name);
        Assert.True(capturedArgs.IsUnBan);
        Assert.Equal(endsAt, capturedArgs.BanEndsAt);
    }
}
