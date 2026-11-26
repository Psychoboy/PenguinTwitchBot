using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PenguinTwitchBot.Bot.Commands.Music;
using PenguinTwitchBot.Bot.Hubs;

namespace PenguinTwitchBot.Test.Bot.Hubs;

public class InProcessHubEventBusTests
{
    private sealed class DeathCounterPayload
    {
        public string? Game { get; set; }
        public int Amount { get; set; }
    }

    [Fact]
    public async Task PublishAsync_DeliversToTypedSubscribers_CaseInsensitively()
    {
        var bus = new InProcessHubEventBus(NullLogger<InProcessHubEventBus>.Instance);
        string? receivedPrize = null;
        (int entries, int entrants) receivedTickets = default;

        using var sub1 = bus.On<string>(HubNames.MainHub, "Prize", prize => receivedPrize = prize);
        using var sub2 = bus.On<int, int>("mainhub", "updatetickets", (entries, entrants) => receivedTickets = (entries, entrants));

        await bus.PublishAsync(HubNames.MainHub, "prize", ["Steam Deck"]);
        await bus.PublishAsync("MAINHUB", "UpdateTickets", [42, 17]);

        Assert.Equal("Steam Deck", receivedPrize);
        Assert.Equal((42, 17), receivedTickets);
    }

    [Fact]
    public async Task PublishAsync_ConvertsAnonymousObjectsToTargetDto()
    {
        var bus = new InProcessHubEventBus(NullLogger<InProcessHubEventBus>.Instance);
        DeathCounterPayload? received = null;

        using var sub = bus.On<DeathCounterPayload>(HubNames.MainHub, "DeathCounterUpdated", payload => received = payload);

        await bus.PublishAsync(HubNames.MainHub, "DeathCounterUpdated", [new { game = "Elden Ring", amount = 99 }]);

        Assert.NotNull(received);
        Assert.Equal("Elden Ring", received!.Game);
        Assert.Equal(99, received.Amount);
    }

    [Fact]
    public async Task Dispose_UnsubscribesAndIsIdempotent()
    {
        var bus = new InProcessHubEventBus(NullLogger<InProcessHubEventBus>.Instance);
        var count = 0;

        var sub = bus.On<bool>(HubNames.MainHub, "StreamChanged", _ => count++);

        await bus.PublishAsync(HubNames.MainHub, "StreamChanged", [true]);
        sub.Dispose();
        sub.Dispose();
        await bus.PublishAsync(HubNames.MainHub, "StreamChanged", [false]);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task PublishAsync_IsolatesSubscriberExceptions()
    {
        var bus = new InProcessHubEventBus(NullLogger<InProcessHubEventBus>.Instance);
        var secondCalled = false;

        using var sub1 = bus.On<string>(HubNames.MainHub, "Prize", _ => throw new InvalidOperationException("boom"));
        using var sub2 = bus.On<string>(HubNames.MainHub, "Prize", _ => secondCalled = true);

        await bus.PublishAsync(HubNames.MainHub, "Prize", ["RTX 5090"]);

        Assert.True(secondCalled);
    }

    [Fact]
    public async Task InProcessBroadcastingHubContext_ForwardsToSignalRAndEventBus()
    {
        var innerProxy = new Mock<IClientProxy>();
        var innerClients = new Mock<IHubClients>();
        innerClients.SetupGet(c => c.All).Returns(innerProxy.Object);
        var innerContext = new Mock<IHubContext<MainHub>>();
        innerContext.SetupGet(c => c.Clients).Returns(innerClients.Object);

        var bus = new InProcessHubEventBus(NullLogger<InProcessHubEventBus>.Instance);
        bool? receivedOnline = null;
        using var sub = bus.On<bool>(HubNames.MainHub, "StreamChanged", status => receivedOnline = status);

        var bridgeContext = new InProcessBroadcastingHubContext<MainHub>(innerContext.Object, bus);
        await bridgeContext.Clients.All.SendAsync("StreamChanged", true);

        Assert.True(receivedOnline);
        innerProxy.Verify(
            p => p.SendCoreAsync("StreamChanged", It.Is<object?[]>(a => a.Length == 1 && (bool)a[0]! == true), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void AddInProcessHubBridge_ResolvesBroadcastingHubContextForMainHubAndYtHub()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        services.AddSingleton<IInProcessHubEventBus, InProcessHubEventBus>();
        services.AddInProcessHubBridge<MainHub>();
        services.AddInProcessHubBridge<YtHub>();

        using var provider = services.BuildServiceProvider();

        var mainHubContext = provider.GetRequiredService<IHubContext<MainHub>>();
        var ytHubContext = provider.GetRequiredService<IHubContext<YtHub>>();

        Assert.IsType<InProcessBroadcastingHubContext<MainHub>>(mainHubContext);
        Assert.IsType<InProcessBroadcastingHubContext<YtHub>>(ytHubContext);
    }
}
