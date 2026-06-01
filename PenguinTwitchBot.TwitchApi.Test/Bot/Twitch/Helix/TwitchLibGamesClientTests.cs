using Microsoft.Extensions.Logging;
using PenguinTwitchBot.TwitchApi.Helix;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TwitchLib.Api.Helix.Models.Games;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class GamesClientTests
{
    [Fact]
    public async Task GetGamesAsync_SuccessfulCall_ReturnsResponse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<GamesClient>>();
        var transport = Substitute.For<IGamesTransport>();
        
        var expectedResponse = new GetGamesResponse();
        transport.GetGamesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(Task.FromResult(expectedResponse));

        var client = new GamesClient(logger, transport);

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
        var logger = Substitute.For<ILogger<GamesClient>>();
        var transport = Substitute.For<IGamesTransport>();
        
        var expectedResponse = new GetGamesResponse();
        
        transport.GetGamesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(
                Task.FromException<GetGamesResponse>(new HttpRequestException("Transient")),
                Task.FromResult(expectedResponse)
            );

        var client = new GamesClient(logger, transport);

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
        var logger = Substitute.For<ILogger<GamesClient>>();
        var transport = Substitute.For<IGamesTransport>();
        
        var exception = new InvalidOperationException("Non-transient error");
        transport.GetGamesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(Task.FromException<GetGamesResponse>(exception));

        var client = new GamesClient(logger, transport);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            client.GetGamesAsync("client", "token", new List<string> { "game123" }));
    }
}
