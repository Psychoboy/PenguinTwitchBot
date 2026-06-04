using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.EventSub;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class ModerationEventSubClientTests
{
    [Fact]
    public async Task CreateEventSubSubscriptionAsync_ShouldReturnEnabled_WhenTransportSucceeds()
    {
        var logger = Substitute.For<ILogger<ModerationClient>>();
        var transport = Substitute.For<IModerationTransport>();
        var response = CreateEventSubResponse();

        transport.CreateEventSubSubscriptionAsync(
            "cid",
            "token",
            "channel.chat.message",
            "1",
            Arg.Any<Dictionary<string, string>>(),
            EventSubTransportMethod.Websocket,
            "session")
            .Returns(response);

        var sut = new ModerationClient(logger, transport);

        var result = await sut.CreateEventSubSubscriptionAsync(
            "cid",
            "token",
            "channel.chat.message",
            "1",
            new Dictionary<string, string> { { "broadcaster_user_id", "1" } },
            EventSubTransportMethod.Websocket,
            "session");

        Assert.True(result.IsEnabled);
    }

    [Fact]
    public async Task CreateEventSubSubscriptionAsync_ShouldRetryTransientFailures_AndSucceed()
    {
        var logger = Substitute.For<ILogger<ModerationClient>>();
        var transport = Substitute.For<IModerationTransport>();
        var attempts = 0;

        transport.CreateEventSubSubscriptionAsync(
            "cid",
            "token",
            "channel.chat.message",
            "1",
            Arg.Any<Dictionary<string, string>>(),
            EventSubTransportMethod.Websocket,
            "session")
            .Returns(_ =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new TimeoutException("network timeout");
                }

                return CreateEventSubResponse();
            });

        var sut = new ModerationClient(logger, transport);

        var result = await sut.CreateEventSubSubscriptionAsync(
            "cid",
            "token",
            "channel.chat.message",
            "1",
            new Dictionary<string, string> { { "broadcaster_user_id", "1" } },
            EventSubTransportMethod.Websocket,
            "session");

        Assert.NotNull(result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task DeleteChatMessagesAsync_ShouldThrowImmediately_OnNonTransientFailure()
    {
        var logger = Substitute.For<ILogger<ModerationClient>>();
        var transport = Substitute.For<IModerationTransport>();
        var attempts = 0;

        transport.DeleteChatMessagesAsync("cid", "token", "broadcaster", "moderator", "msg-id")
            .Returns(_ =>
            {
                attempts++;
                return Task.FromException(new InvalidOperationException("bad state"));
            });

        var sut = new ModerationClient(logger, transport);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DeleteChatMessagesAsync("cid", "token", "broadcaster", "moderator", "msg-id"));
        Assert.Equal(1, attempts);
    }

    private static EventSubSubscriptionResult CreateEventSubResponse()
    {
        return new EventSubSubscriptionResult(true);
    }
}
