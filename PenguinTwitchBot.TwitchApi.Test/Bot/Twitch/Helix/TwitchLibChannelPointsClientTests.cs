using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;
using Microsoft.Extensions.Logging;
using NSubstitute;

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
            .Returns(Task.FromResult(response));

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

                return Task.FromResult(CreateGetCustomRewardsResponse());
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

        transport.CreateCustomRewardsAsync("cid", "token", "broadcaster", Arg.Any<CreateChannelPointRewardRequest>())
            .Returns(_ =>
            {
                attempts++;
                return Task.FromException(new InvalidOperationException("bad state"));
            });

        var sut = new ChannelPointsClient(logger, transport);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateCustomRewardsAsync("cid", "token", "broadcaster", new CreateChannelPointRewardRequest()));
        Assert.Equal(1, attempts);
    }

    private static GetChannelPointRewardsResponse CreateGetCustomRewardsResponse()
    {
        return new GetChannelPointRewardsResponse([
            new ChannelPointReward(
                Id: "reward-id",
                Title: "Reward",
                IsEnabled: true,
                IsPaused: false,
                Cost: 100,
                Prompt: null,
                IsUserInputRequired: false,
                BackgroundColor: null,
                ShouldRedemptionsSkipQueue: false,
                IsMaxPerStreamEnabled: null,
                MaxPerStream: null,
                IsMaxPerUserPerStreamEnabled: null,
                MaxPerUserPerStream: null,
                IsGlobalCooldownEnabled: null,
                GlobalCooldownSeconds: null)
        ]);
    }
}
