using Microsoft.Extensions.Logging;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.Clips;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class ClipsClientTests
{
    [Fact]
    public async Task GetClipsAsync_SuccessfulCall_ReturnsResponse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ClipsClient>>();
        var transport = Substitute.For<IClipsTransport>();
        
        var expectedResponse = new GetClipsResponse([]);
        transport.GetClipsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<bool?>())
            .Returns(Task.FromResult(expectedResponse));

        var client = new ClipsClient(logger, transport);

        // Act
        var result = await client.GetClipsAsync("client", "token", "broadcaster123", "user456", 100, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GetClipsByIdAsync_TransientError_RetriesAndSucceeds()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ClipsClient>>();
        var transport = Substitute.For<IClipsTransport>();
        
        var expectedResponse = new GetClipsResponse([]);
        
        transport.GetClipsByIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(
                Task.FromException<GetClipsResponse>(new TimeoutException("Transient")),
                Task.FromResult(expectedResponse)
            );

        var client = new ClipsClient(logger, transport);

        // Act
        var result = await client.GetClipsByIdAsync("client", "token", new List<string> { "clip123" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result);
        await transport.Received(2).GetClipsByIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>());
    }

    [Fact]
    public async Task GetClipsAsync_NonTransientError_ThrowsImmediately()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ClipsClient>>();
        var transport = Substitute.For<IClipsTransport>();
        
        var exception = new NotSupportedException("Non-transient error");
        transport.GetClipsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<bool?>())
            .Returns(Task.FromException<GetClipsResponse>(exception));

        var client = new ClipsClient(logger, transport);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => 
            client.GetClipsAsync("client", "token", "broadcaster123", "user456", 100, null));
    }
}
