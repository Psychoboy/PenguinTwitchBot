using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Helpers;
using PenguinTwitchBot.TwitchApi.Auth;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.Auth;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.TwitchServices;

public class TwitchServiceStatusTests
{
    private readonly ILogger<TwitchService> _logger = Substitute.For<ILogger<TwitchService>>();
    private readonly IConfiguration _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["twitchAccessToken"] = "initial-token",
        ["twitchRefreshToken"] = "refresh-token",
        ["twitchClientId"] = "client-id",
        ["twitchClientSecret"] = "client-secret"
    }).Build();
    private readonly IAuthClient _authClient = Substitute.For<IAuthClient>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IChannelPointsClient _channelPointsClient = Substitute.For<IChannelPointsClient>();
    private readonly IModerationClient _moderationClient = Substitute.For<IModerationClient>();
    private readonly IChannelsClient _channelsClient = Substitute.For<IChannelsClient>();
    private readonly IStreamsClient _streamsClient = Substitute.For<IStreamsClient>();
    private readonly IClipsClient _clipsClient = Substitute.For<IClipsClient>();
    private readonly IGamesClient _gamesClient = Substitute.For<IGamesClient>();
    private readonly ISubscriptionsClient _subscriptionsClient = Substitute.For<ISubscriptionsClient>();
    private readonly IRaidsClient _raidsClient = Substitute.For<IRaidsClient>();
    private readonly IUsersClient _usersClient = Substitute.For<IUsersClient>();
    private readonly IScheduleClient _scheduleClient = Substitute.For<IScheduleClient>();
    private readonly IPenguinDispatcher _dispatcher = Substitute.For<IPenguinDispatcher>();
    private readonly IChatMessageIdTracker _messageIdTracker = Substitute.For<IChatMessageIdTracker>();

    private TwitchService CreateSut()
    {
        var settingsManager = new SettingsFileManager(Substitute.For<ILogger<SettingsFileManager>>(), _config);
        return new TwitchService(
            _logger,
            _config,
            _authClient,
            _chatClient,
            _channelPointsClient,
            _moderationClient,
            _channelsClient,
            _streamsClient,
            _clipsClient,
            _gamesClient,
            _subscriptionsClient,
            _raidsClient,
            _usersClient,
            _scheduleClient,
            settingsManager,
            _dispatcher,
            _messageIdTracker);
    }

    [Fact]
    public void InitialState_IsServiceUpFalse_AndStatusUnknown()
    {
        var sut = CreateSut();
        Assert.False(sut.IsServiceUp());
        Assert.Equal(TwitchServiceStatus.Unknown, sut.GetStatus());
    }

    [Fact]
    public async Task ValidateAndRefreshToken_WhenValid_RaisesServiceStatusChangedWithConnected()
    {
        var sut = CreateSut();
        _authClient.ValidateAccessTokenAsync(Arg.Any<string>())
            .Returns(new TokenValidation(3600));

        TwitchServiceStatus? eventArg = null;
        int eventCount = 0;
        sut.ServiceStatusChanged += (sender, status) =>
        {
            eventCount++;
            eventArg = status;
        };

        var result = await sut.ValidateAndRefreshToken();

        Assert.True(result);
        Assert.True(sut.IsServiceUp());
        Assert.Equal(TwitchServiceStatus.Connected, sut.GetStatus());
        Assert.Equal(1, eventCount);
        Assert.Equal(TwitchServiceStatus.Connected, eventArg);
    }

    [Fact]
    public async Task ValidateAndRefreshToken_WhenUnauthorizedAndCannotRefresh_RaisesAuthenticationDisconnected()
    {
        var sut = CreateSut();
        // First make it valid
        _authClient.ValidateAccessTokenAsync(Arg.Any<string>())
            .Returns(new TokenValidation(3600));
        await sut.ValidateAndRefreshToken();
        Assert.True(sut.IsServiceUp());
        Assert.Equal(TwitchServiceStatus.Connected, sut.GetStatus());

        // Now make it unauthorized and refresh fails
        _authClient.ValidateAccessTokenAsync(Arg.Any<string>())
            .ThrowsAsync(new HttpRequestException("unauthorized", null, System.Net.HttpStatusCode.Unauthorized));
        _authClient.RefreshAuthTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new Exception("cannot refresh"));

        TwitchServiceStatus? eventArg = null;
        int eventCount = 0;
        sut.ServiceStatusChanged += (sender, status) =>
        {
            eventCount++;
            eventArg = status;
        };

        var result = await sut.ValidateAndRefreshToken();

        Assert.False(result);
        Assert.False(sut.IsServiceUp());
        Assert.Equal(TwitchServiceStatus.AuthenticationDisconnected, sut.GetStatus());
        Assert.Equal(1, eventCount);
        Assert.Equal(TwitchServiceStatus.AuthenticationDisconnected, eventArg);
    }

    [Fact]
    public async Task ValidateAndRefreshToken_WhenNetworkError_RaisesUnavailable()
    {
        var sut = CreateSut();
        _authClient.ValidateAccessTokenAsync(Arg.Any<string>())
            .ThrowsAsync(new HttpRequestException("Service Unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable));

        TwitchServiceStatus? eventArg = null;
        int eventCount = 0;
        sut.ServiceStatusChanged += (sender, status) =>
        {
            eventCount++;
            eventArg = status;
        };

        var result = await sut.ValidateAndRefreshToken();

        Assert.False(result);
        Assert.False(sut.IsServiceUp());
        Assert.Equal(TwitchServiceStatus.Unavailable, sut.GetStatus());
        Assert.Equal(1, eventCount);
        Assert.Equal(TwitchServiceStatus.Unavailable, eventArg);
    }

    [Fact]
    public async Task ValidateAndRefreshToken_WhenEmptyToken_SetsAuthenticationDisconnected()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["twitchAccessToken"] = "",
            ["twitchRefreshToken"] = "",
            ["twitchClientId"] = "client-id",
            ["twitchClientSecret"] = "client-secret"
        }).Build();
        var settingsManager = new SettingsFileManager(Substitute.For<ILogger<SettingsFileManager>>(), config);
        var sut = new TwitchService(
            _logger,
            config,
            _authClient,
            _chatClient,
            _channelPointsClient,
            _moderationClient,
            _channelsClient,
            _streamsClient,
            _clipsClient,
            _gamesClient,
            _subscriptionsClient,
            _raidsClient,
            _usersClient,
            _scheduleClient,
            settingsManager,
            _dispatcher,
            _messageIdTracker);

        var result = await sut.ValidateAndRefreshToken();

        Assert.False(result);
        Assert.False(sut.IsServiceUp());
        Assert.Equal(TwitchServiceStatus.AuthenticationDisconnected, sut.GetStatus());
    }

    [Fact]
    public async Task ValidateAndRefreshToken_WhenStatusDoesNotChange_DoesNotRaiseServiceStatusChanged()
    {
        var sut = CreateSut();
        _authClient.ValidateAccessTokenAsync(Arg.Any<string>())
            .Returns(new TokenValidation(3600));

        // First transition from Unknown -> Connected
        await sut.ValidateAndRefreshToken();

        int eventCount = 0;
        sut.ServiceStatusChanged += (sender, status) => eventCount++;

        // Second call still Connected
        await sut.ValidateAndRefreshToken();

        Assert.Equal(0, eventCount);
    }
}
