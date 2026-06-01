using Microsoft.Extensions.Logging;
using PenguinTwitchBot.TwitchApi.Helix;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class StreamsClientTests
{
    [Fact]
    public async Task GetStreamsAsync_SuccessfulCall_ReturnsResponse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<StreamsClient>>();
        var transport = Substitute.For<IStreamsTransport>();
        
        var expectedResponse = new GetStreamsResponse();
        transport.GetStreamsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(Task.FromResult(expectedResponse));

        var client = new StreamsClient(logger, transport);

        // Act
        var result = await client.GetStreamsAsync("client", "token", new List<string> { "user123" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GetStreamsAsync_TransientError_RetriesAndSucceeds()
    {
        // Arrange
        var logger = Substitute.For<ILogger<StreamsClient>>();
        var transport = Substitute.For<IStreamsTransport>();
        
        var expectedResponse = new GetStreamsResponse();
        
        transport.GetStreamsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(
                Task.FromException<GetStreamsResponse>(new TaskCanceledException("Transient")),
                Task.FromResult(expectedResponse)
            );

        var client = new StreamsClient(logger, transport);

        // Act
        var result = await client.GetStreamsAsync("client", "token", new List<string> { "user123" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result);
        await transport.Received(2).GetStreamsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>());
    }

    [Fact]
    public async Task GetStreamsAsync_NonTransientError_ThrowsImmediately()
    {
        // Arrange
        var logger = Substitute.For<ILogger<StreamsClient>>();
        var transport = Substitute.For<IStreamsTransport>();
        
        var exception = new UnauthorizedAccessException("Non-transient error");
        transport.GetStreamsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(Task.FromException<GetStreamsResponse>(exception));

        var client = new StreamsClient(logger, transport);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            client.GetStreamsAsync("client", "token", new List<string> { "user123" }));
    }
}
