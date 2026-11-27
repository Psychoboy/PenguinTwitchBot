using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.Subscriptions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
        
        var expectedResponse = new CheckUserSubscriptionResponse([]);
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
        
        var expectedResponse = new GetBroadcasterSubscriptionsResponse([], null, 0, 0);
        
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

    [Fact]
    public async Task SubscriptionsTransport_GetBroadcasterSubscriptions_MapsTierGiftAndTotals()
    {
        const string json = """
            {
              "data": [
                {
                  "broadcaster_id": "100",
                  "broadcaster_login": "streamer",
                  "broadcaster_name": "Streamer",
                  "gifter_id": "200",
                  "gifter_login": "kindgifter",
                  "gifter_name": "KindGifter",
                  "is_gift": true,
                  "tier": "2000",
                  "plan_name": "Channel Subscription (Tier 2)",
                  "user_id": "300",
                  "user_login": "luckyviewer",
                  "user_name": "LuckyViewer"
                }
              ],
              "pagination": { "cursor": "next-cursor" },
              "total": 42,
              "points": 55
            }
            """;

        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));

        var transport = new SubscriptionsTransport(httpClientFactory);
        var response = await transport.GetBroadcasterSubscriptionsAsync("client", "token", "100", 100, null);

        Assert.Equal(42, response.Total);
        Assert.Equal(55, response.Points);
        Assert.Equal("next-cursor", response.Cursor);
        var sub = Assert.Single(response.Data);
        Assert.Equal("300", sub.UserId);
        Assert.Equal("luckyviewer", sub.UserLogin);
        Assert.Equal("LuckyViewer", sub.UserName);
        Assert.Equal("2000", sub.Tier);
        Assert.True(sub.IsGift);
        Assert.Equal("200", sub.GifterId);
        Assert.Equal("kindgifter", sub.GifterLogin);
        Assert.Equal("KindGifter", sub.GifterName);
        Assert.Equal("Channel Subscription (Tier 2)", sub.PlanName);
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
