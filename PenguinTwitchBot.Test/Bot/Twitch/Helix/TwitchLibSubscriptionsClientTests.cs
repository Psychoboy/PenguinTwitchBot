using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TwitchLib.Api.Helix.Models.Subscriptions;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class TwitchLibSubscriptionsClientTests
{
    [Fact]
    public async Task CheckUserSubscriptionAsync_SuccessfulCall_ReturnsResponse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<TwitchLibSubscriptionsClient>>();
        var transport = Substitute.For<ITwitchSubscriptionsTransport>();
        
        var expectedResponse = new CheckUserSubscriptionResponse
        {
            Data = new[] { new Subscription { UserId = "user123" } }
        };
        transport.CheckUserSubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(expectedResponse));

        var client = new TwitchLibSubscriptionsClient(logger, transport);

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
        var logger = Substitute.For<ILogger<TwitchLibSubscriptionsClient>>();
        var transport = Substitute.For<ITwitchSubscriptionsTransport>();
        
        var expectedResponse = new GetBroadcasterSubscriptionsResponse
        {
            Data = new[] { new Subscription { UserId = "user123" } },
            Pagination = new() { Cursor = null }
        };
        
        transport.GetBroadcasterSubscriptionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>())
            .Returns(
                Task.FromException<GetBroadcasterSubscriptionsResponse>(new TaskCanceledException("Transient")),
                Task.FromResult(expectedResponse)
            );

        var client = new TwitchLibSubscriptionsClient(logger, transport);

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
        var logger = Substitute.For<ILogger<TwitchLibSubscriptionsClient>>();
        var transport = Substitute.For<ITwitchSubscriptionsTransport>();
        
        var exception = new UnauthorizedAccessException("Non-transient error");
        transport.CheckUserSubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException<CheckUserSubscriptionResponse>(exception));

        var client = new TwitchLibSubscriptionsClient(logger, transport);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            client.CheckUserSubscriptionAsync("client", "token", "broadcaster123", "user456"));
    }
}
