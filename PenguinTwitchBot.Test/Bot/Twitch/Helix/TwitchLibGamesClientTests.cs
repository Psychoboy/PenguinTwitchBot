using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TwitchLib.Api.Helix.Models.Games;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class TwitchLibGamesClientTests
{
    [Fact]
    public async Task GetGamesAsync_SuccessfulCall_ReturnsResponse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<TwitchLibGamesClient>>();
        var transport = Substitute.For<ITwitchGamesTransport>();
        
        var expectedResponse = new GetGamesResponse
        {
            Data = new[] { new Game { Name = "Test Game" } }
        };
        transport.GetGamesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(Task.FromResult(expectedResponse));

        var client = new TwitchLibGamesClient(logger, transport);

        // Act
        var result = await client.GetGamesAsync("client", "token", new List<string> { "game123" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GetGamesAsync_TransientError_RetriesAndSucceeds()
    {
        // Arrange
        var logger = Substitute.For<ILogger<TwitchLibGamesClient>>();
        var transport = Substitute.For<ITwitchGamesTransport>();
        
        var expectedResponse = new GetGamesResponse
        {
            Data = new[] { new Game { Name = "Test Game" } }
        };
        
        transport.GetGamesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(
                Task.FromException<GetGamesResponse>(new HttpRequestException("Transient")),
                Task.FromResult(expectedResponse)
            );

        var client = new TwitchLibGamesClient(logger, transport);

        // Act
        var result = await client.GetGamesAsync("client", "token", new List<string> { "game123" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result);
        await transport.Received(2).GetGamesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>());
    }

    [Fact]
    public async Task GetGamesAsync_NonTransientError_ThrowsImmediately()
    {
        // Arrange
        var logger = Substitute.For<ILogger<TwitchLibGamesClient>>();
        var transport = Substitute.For<ITwitchGamesTransport>();
        
        var exception = new InvalidOperationException("Non-transient error");
        transport.GetGamesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(Task.FromException<GetGamesResponse>(exception));

        var client = new TwitchLibGamesClient(logger, transport);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            client.GetGamesAsync("client", "token", new List<string> { "game123" }));
    }
}
