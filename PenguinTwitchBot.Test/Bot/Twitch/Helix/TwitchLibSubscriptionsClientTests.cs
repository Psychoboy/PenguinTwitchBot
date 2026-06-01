using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Twitch.Helix;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TwitchLib.Api.Helix.Models.Subscriptions;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class SubscriptionsClientTests
{
    [Fact]
    public async Task CheckUserSubscriptionAsync_SuccessfulCall_ReturnsResponse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SubscriptionsClient>>();
        var transport = Substitute.For<ISubscriptionsTransport>();
        
        var expectedResponse = new CheckUserSubscriptionResponse();
        transport.CheckUserSubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(expectedResponse));

        var client = new SubscriptionsClient(logger, transport);

        // Act
        var result = await client.CheckUserSubscriptionAsync("client", "token", "broadcaster123", "user456");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GetBroadcasterSubscriptionsAsync_TransientError_RetriesAndSucceeds()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SubscriptionsClient>>();
        var transport = Substitute.For<ISubscriptionsTransport>();
        
        var expectedResponse = new GetBroadcasterSubscriptionsResponse();
        
        transport.GetBroadcasterSubscriptionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>())
            .Returns(
                Task.FromException<GetBroadcasterSubscriptionsResponse>(new TaskCanceledException("Transient")),
                Task.FromResult(expectedResponse)
            );

        var client = new SubscriptionsClient(logger, transport);

        // Act
        var result = await client.GetBroadcasterSubscriptionsAsync("client", "token", "broadcaster123", 100, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result);
        await transport.Received(2).GetBroadcasterSubscriptionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CheckUserSubscriptionAsync_NonTransientError_ThrowsImmediately()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SubscriptionsClient>>();
        var transport = Substitute.For<ISubscriptionsTransport>();
        
        var exception = new UnauthorizedAccessException("Non-transient error");
        transport.CheckUserSubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException<CheckUserSubscriptionResponse>(exception));

        var client = new SubscriptionsClient(logger, transport);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            client.CheckUserSubscriptionAsync("client", "token", "broadcaster123", "user456"));
    }
}
