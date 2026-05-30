using PenguinTwitchBot.Bot.Twitch.Helix;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TwitchLib.Api.Helix.Models.Channels.SendChatMessage;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class TwitchLibChatClientTests
{
    [Fact]
    public async Task SendChatMessageAsync_ShouldReturnResponse_WhenTransportSucceeds()
    {
        var logger = Substitute.For<ILogger<TwitchLibChatClient>>();
        var transport = Substitute.For<ITwitchChatTransport>();
        var response = CreateSendChatMessageResponse();

        transport.SendChatMessageAsync("cid", "token", Arg.Any<SendChatMessageRequest>()).Returns(response);

        var sut = new TwitchLibChatClient(logger, transport);
        var result = await sut.SendChatMessageAsync("cid", "token", new SendChatMessageRequest
        {
            BroadcasterId = "1",
            SenderId = "1",
            Message = "hello"
        });

        Assert.Same(response, result);
        Assert.Single(result.Data);
    }

    [Fact]
    public async Task SendChatMessageAsync_ShouldRetryTransientFailures_AndSucceed()
    {
        var logger = Substitute.For<ILogger<TwitchLibChatClient>>();
        var transport = Substitute.For<ITwitchChatTransport>();
        var attempts = 0;

        transport.SendChatMessageAsync("cid", "token", Arg.Any<SendChatMessageRequest>())
            .Returns(_ =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new HttpRequestException("network issue");
                }

                return CreateSendChatMessageResponse();
            });

        var sut = new TwitchLibChatClient(logger, transport);
        var result = await sut.SendChatMessageAsync("cid", "token", new SendChatMessageRequest
        {
            BroadcasterId = "1",
            SenderId = "1",
            Message = "hello"
        });

        Assert.NotNull(result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task SendChatMessageAsync_ShouldThrowImmediately_OnNonTransientFailure()
    {
        var logger = Substitute.For<ILogger<TwitchLibChatClient>>();
        var transport = Substitute.For<ITwitchChatTransport>();
        var attempts = 0;

        transport.SendChatMessageAsync("cid", "token", Arg.Any<SendChatMessageRequest>())
            .Returns(_ =>
            {
                attempts++;
                return Task.FromException<SendChatMessageResponse>(new InvalidOperationException("bad state"));
            });

        var sut = new TwitchLibChatClient(logger, transport);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendChatMessageAsync("cid", "token", new SendChatMessageRequest
        {
            BroadcasterId = "1",
            SenderId = "1",
            Message = "hello"
        }));
        Assert.Equal(1, attempts);
    }

    private static SendChatMessageResponse CreateSendChatMessageResponse()
    {
        const string json = "{\"data\":[{\"message_id\":\"abc\",\"is_sent\":true}]}";
        return Newtonsoft.Json.JsonConvert.DeserializeObject<SendChatMessageResponse>(json)!;
    }
}
