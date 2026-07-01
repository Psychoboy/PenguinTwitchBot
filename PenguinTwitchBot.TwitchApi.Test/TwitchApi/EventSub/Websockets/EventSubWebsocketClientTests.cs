using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets.Client;
using Xunit;

namespace PenguinTwitchBot.Test.TwitchApi.EventSub.Websockets;

public class EventSubWebsocketClientTests
{
    private readonly ILogger<EventSubWebsocketClient> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebsocketClient _websocketClient;

    public EventSubWebsocketClientTests()
    {
        _logger = NullLogger<EventSubWebsocketClient>.Instance;
        _websocketClient = Substitute.For<IWebsocketClient>();
        var services = new ServiceCollection();
        services.AddSingleton(_websocketClient);
        _serviceProvider = services.BuildServiceProvider();
    }

    private EventSubWebsocketClient CreateClient()
    {
        return new EventSubWebsocketClient(_logger, _serviceProvider, _websocketClient);
    }

    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        typeof(EventSubWebsocketClient)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(obj, value);
    }

    [Fact]
    public async Task ConnectAsync_NoArgs_CallsConnectAsyncWithDefaultUrl()
    {
        var client = CreateClient();
        _websocketClient.IsConnected.Returns(false, true);
        _websocketClient.ConnectAsync(Arg.Any<Uri>()).Returns(true);

        var result = await client.ConnectAsync();

        Assert.True(result);
        await _websocketClient.Received(1).ConnectAsync(Arg.Is<Uri>(u => u.ToString().Contains("eventsub.wss.twitch.tv")));
    }

    [Fact]
    public async Task ConnectAsync_WithUrl_UsesProvidedUrl()
    {
        var client = CreateClient();
        var url = new Uri("wss://custom.example.com/ws");
        _websocketClient.IsConnected.Returns(false, true);
        _websocketClient.ConnectAsync(Arg.Any<Uri>()).Returns(true);

        var result = await client.ConnectAsync(url);

        Assert.True(result);
        await _websocketClient.Received(1).ConnectAsync(url);
    }

    [Fact]
    public async Task ConnectAsync_WithNullUrl_UsesDefaultUrl()
    {
        var client = CreateClient();
        _websocketClient.IsConnected.Returns(false, true);
        _websocketClient.ConnectAsync(Arg.Any<Uri>()).Returns(true);

        var result = await client.ConnectAsync(null);

        Assert.True(result);
        await _websocketClient.Received(1).ConnectAsync(Arg.Is<Uri>(u => u.ToString().Contains("eventsub.wss.twitch.tv")));
    }

    [Fact]
    public async Task ConnectAsync_WhenConnectFails_ReturnsFalse()
    {
        var client = CreateClient();
        _websocketClient.IsConnected.Returns(false);
        _websocketClient.ConnectAsync(Arg.Any<Uri>()).Returns(false);

        var result = await client.ConnectAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task ConnectAsync_WhenAlreadyConnected_ReturnsTrueWithoutMonitor()
    {
        var client = CreateClient();
        _websocketClient.IsConnected.Returns(true);
        _websocketClient.ConnectAsync(Arg.Any<Uri>()).Returns(true);

        var result = await client.ConnectAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task ReconnectAsync_NoArgs_DelegatesToReconnectAsync()
    {
        var client = CreateClient();
        _websocketClient.IsConnected.Returns(false);
        _websocketClient.ConnectAsync(Arg.Any<Uri>()).Returns(true);

        SetPrivateField(client, "_reconnectRequested", false);

        var result = await client.ReconnectAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task ReconnectAsync_WithCancellation_ReturnsFalse()
    {
        var client = CreateClient();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await client.ReconnectAsync(cts.Token);

        Assert.False(result);
    }

    [Fact]
    public async Task ReconnectAsync_WhenReconnectRequestedAndSucceeds_ReturnsTrue()
    {
        var client = CreateClient();
        SetPrivateField(client, "_reconnectRequested", true);
        SetPrivateField(client, "_reconnectComplete", true);
        _websocketClient.IsConnected.Returns(true);
        _websocketClient.ConnectAsync(Arg.Any<Uri>()).Returns(true);
        _websocketClient.DisconnectAsync().Returns(true);

        var result = await client.ReconnectAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task ReconnectAsync_WhenReconnectRequestedAndConnectFails_ReturnsFalse()
    {
        var client = CreateClient();
        SetPrivateField(client, "_reconnectRequested", true);
        _websocketClient.IsConnected.Returns(false);
        _websocketClient.ConnectAsync(Arg.Any<Uri>()).Returns(false);

        var result = await client.ReconnectAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task ReconnectAsync_WhenNotReconnectRequested_DisconnectsAndReconnects()
    {
        var secondClient = Substitute.For<IWebsocketClient>();
        var services = new ServiceCollection();
        services.AddSingleton(secondClient);
        var sp = services.BuildServiceProvider();
        var client = new EventSubWebsocketClient(_logger, sp, _websocketClient);
        SetPrivateField(client, "_reconnectRequested", false);
        _websocketClient.IsConnected.Returns(true);
        _websocketClient.DisconnectAsync().Returns(true);
        secondClient.IsConnected.Returns(false);
        secondClient.ConnectAsync(Arg.Any<Uri>()).Returns(true);

        var result = await client.ReconnectAsync();

        Assert.True(result);
        await _websocketClient.Received(1).DisconnectAsync();
        await secondClient.Received(1).ConnectAsync(Arg.Any<Uri>());
    }

    [Fact]
    public async Task ReconnectAsync_WhenNotReconnectRequestedAndAlreadyDisconnected_Reconnects()
    {
        var client = CreateClient();
        SetPrivateField(client, "_reconnectRequested", false);
        _websocketClient.IsConnected.Returns(false);
        _websocketClient.ConnectAsync(Arg.Any<Uri>()).Returns(true);

        var result = await client.ReconnectAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task ReconnectAsync_WhenNotReconnectRequested_Cancelled_ReturnsFalse()
    {
        var client = CreateClient();
        SetPrivateField(client, "_reconnectRequested", false);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await client.ReconnectAsync(cts.Token);

        Assert.False(result);
    }
}
