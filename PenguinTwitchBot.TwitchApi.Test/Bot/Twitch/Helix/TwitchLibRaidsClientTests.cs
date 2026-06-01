using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Twitch.Helix;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class RaidsClientTests
{
    [Fact]
    public async Task StartRaidAsync_SuccessfulCall_CompletsWithoutThrow()
    {
        // Arrange
        var logger = Substitute.For<ILogger<RaidsClient>>();
        var transport = Substitute.For<IRaidsTransport>();
        
        transport.StartRaidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var client = new RaidsClient(logger, transport);

        // Act
        await client.StartRaidAsync("client", "token", "broadcaster123", "target456");

        // Assert
        await transport.Received(1).StartRaidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task StartRaidAsync_TransientError_RetriesAndSucceeds()
    {
        // Arrange
        var logger = Substitute.For<ILogger<RaidsClient>>();
        var transport = Substitute.For<IRaidsTransport>();
        
        transport.StartRaidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(
                Task.FromException(new HttpRequestException("Transient")),
                Task.CompletedTask
            );

        var client = new RaidsClient(logger, transport);

        // Act
        await client.StartRaidAsync("client", "token", "broadcaster123", "target456");

        // Assert
        await transport.Received(2).StartRaidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task StartRaidAsync_NonTransientError_ThrowsImmediately()
    {
        // Arrange
        var logger = Substitute.For<ILogger<RaidsClient>>();
        var transport = Substitute.For<IRaidsTransport>();
        
        var exception = new NotSupportedException("Non-transient error");
        transport.StartRaidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(exception));

        var client = new RaidsClient(logger, transport);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => 
            client.StartRaidAsync("client", "token", "broadcaster123", "target456"));
    }
}
