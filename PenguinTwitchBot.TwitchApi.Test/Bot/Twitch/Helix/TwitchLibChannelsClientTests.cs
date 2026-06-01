using Microsoft.Extensions.Logging;
using PenguinTwitchBot.TwitchApi.Helix;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TwitchLib.Api.Helix.Models.Channels.GetChannelEditors;
using TwitchLib.Api.Helix.Models.Channels.GetChannelFollowers;
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class ChannelsClientTests
{
    [Fact]
    public async Task GetChannelInformationAsync_SuccessfulCall_ReturnsResponse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ChannelsClient>>();
        var transport = Substitute.For<IChannelsTransport>();
        
        var expectedResponse = new GetChannelInformationResponse();
        transport.GetChannelInformationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(expectedResponse));

        var client = new ChannelsClient(logger, transport);

        // Act
        var result = await client.GetChannelInformationAsync("client", "token", "broadcaster123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GetChannelInformationAsync_TransientError_RetriesAndSucceeds()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ChannelsClient>>();
        var transport = Substitute.For<IChannelsTransport>();
        
        var expectedResponse = new GetChannelInformationResponse();
        
        transport.GetChannelInformationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(
                Task.FromException<GetChannelInformationResponse>(new HttpRequestException("Transient")),
                Task.FromResult(expectedResponse)
            );

        var client = new ChannelsClient(logger, transport);

        // Act
        var result = await client.GetChannelInformationAsync("client", "token", "broadcaster123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result);
        await transport.Received(2).GetChannelInformationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetChannelFollowersAsync_SuccessfulCall_ReturnsResponse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ChannelsClient>>();
        var transport = Substitute.For<IChannelsTransport>();
        
        var expectedResponse = new GetChannelFollowersResponse();
        transport.GetChannelFollowersAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>())
            .Returns(Task.FromResult(expectedResponse));

        var client = new ChannelsClient(logger, transport);

        // Act
        var result = await client.GetChannelFollowersAsync("client", "token", "broadcaster123", "user456", 1, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GetChannelEditorsAsync_NonTransientError_ThrowsImmediately()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ChannelsClient>>();
        var transport = Substitute.For<IChannelsTransport>();
        
        var exception = new InvalidOperationException("Non-transient error");
        transport.GetChannelEditorsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException<GetChannelEditorsResponse>(exception));

        var client = new ChannelsClient(logger, transport);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            client.GetChannelEditorsAsync("client", "token", "broadcaster123"));
    }
}
