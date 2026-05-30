using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class TwitchLibRaidsClientTests
{
    [Fact]
    public async Task StartRaidAsync_SuccessfulCall_CompletsWithoutThrow()
    {
        // Arrange
        var logger = Substitute.For<ILogger<TwitchLibRaidsClient>>();
        var transport = Substitute.For<ITwitchRaidsTransport>();
        
        transport.StartRaidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var client = new TwitchLibRaidsClient(logger, transport);

        // Act
        await client.StartRaidAsync("client", "token", "broadcaster123", "target456");

        // Assert
        await transport.Received(1).StartRaidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task StartRaidAsync_TransientError_RetriesAndSucceeds()
    {
        // Arrange
        var logger = Substitute.For<ILogger<TwitchLibRaidsClient>>();
        var transport = Substitute.For<ITwitchRaidsTransport>();
        
        transport.StartRaidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(
                Task.FromException(new HttpRequestException("Transient")),
                Task.CompletedTask
            );

        var client = new TwitchLibRaidsClient(logger, transport);

        // Act
        await client.StartRaidAsync("client", "token", "broadcaster123", "target456");

        // Assert
        await transport.Received(2).StartRaidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task StartRaidAsync_NonTransientError_ThrowsImmediately()
    {
        // Arrange
        var logger = Substitute.For<ILogger<TwitchLibRaidsClient>>();
        var transport = Substitute.For<ITwitchRaidsTransport>();
        
        var exception = new NotSupportedException("Non-transient error");
        transport.StartRaidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(exception));

        var client = new TwitchLibRaidsClient(logger, transport);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => 
            client.StartRaidAsync("client", "token", "broadcaster123", "target456"));
    }
}
