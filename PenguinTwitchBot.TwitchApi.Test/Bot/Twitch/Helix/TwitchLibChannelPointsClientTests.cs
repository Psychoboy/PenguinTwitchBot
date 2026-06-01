using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class ChannelPointsClientTests
{
    [Fact]
    public async Task GetCustomRewardAsync_ShouldReturnResponse_WhenTransportSucceeds()
    {
        var logger = Substitute.For<ILogger<ChannelPointsClient>>();
        var transport = Substitute.For<IChannelPointsTransport>();
        var response = CreateGetCustomRewardsResponse();

        transport.GetCustomRewardAsync("cid", "token", "broadcaster", Arg.Any<List<string>?>(), false)
            .Returns(response);

        var sut = new ChannelPointsClient(logger, transport);

        var result = await sut.GetCustomRewardAsync("cid", "token", "broadcaster", ["reward-id"]);

        Assert.Same(response, result);
        Assert.Single(result.Data);
    }

    [Fact]
    public async Task GetCustomRewardAsync_ShouldRetryTransientFailures_AndSucceed()
    {
        var logger = Substitute.For<ILogger<ChannelPointsClient>>();
        var transport = Substitute.For<IChannelPointsTransport>();
        var attempts = 0;

        transport.GetCustomRewardAsync("cid", "token", "broadcaster", Arg.Any<List<string>?>(), false)
            .Returns(_ =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new HttpRequestException("network issue");
                }

                return CreateGetCustomRewardsResponse();
            });

        var sut = new ChannelPointsClient(logger, transport);
        var result = await sut.GetCustomRewardAsync("cid", "token", "broadcaster", ["reward-id"]);

        Assert.NotNull(result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task CreateCustomRewardsAsync_ShouldThrowImmediately_OnNonTransientFailure()
    {
        var logger = Substitute.For<ILogger<ChannelPointsClient>>();
        var transport = Substitute.For<IChannelPointsTransport>();
        var attempts = 0;

        transport.CreateCustomRewardsAsync("cid", "token", "broadcaster", Arg.Any<CreateCustomRewardsRequest>())
            .Returns(_ =>
            {
                attempts++;
                return Task.FromException<CreateCustomRewardsResponse>(new InvalidOperationException("bad state"));
            });

        var sut = new ChannelPointsClient(logger, transport);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateCustomRewardsAsync("cid", "token", "broadcaster", new CreateChannelPointRewardRequest()));
        Assert.Equal(1, attempts);
    }

    private static GetCustomRewardsResponse CreateGetCustomRewardsResponse()
    {
        const string json = "{\"data\":[{\"id\":\"reward-id\",\"title\":\"Reward\"}]}";
        return Newtonsoft.Json.JsonConvert.DeserializeObject<GetCustomRewardsResponse>(json)!;
    }
}
