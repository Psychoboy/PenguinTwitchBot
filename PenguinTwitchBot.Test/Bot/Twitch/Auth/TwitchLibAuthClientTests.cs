using PenguinTwitchBot.Bot.Twitch.Auth;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace PenguinTwitchBot.Test.Bot.Twitch.Auth;

public class TwitchLibAuthClientTests
{
    [Fact]
    public async Task ExchangeCodeAsync_ShouldReturnToken_WhenTransportReturnsToken()
    {
        var logger = Substitute.For<ILogger<TwitchLibAuthClient>>();
        var transport = Substitute.For<ITwitchAuthTransport>();
        transport.ExchangeCodeAsync("cid", "secret", "code", "redirect")
            .Returns(new TwitchAuthTokenResponse
            {
                AccessToken = "a",
                RefreshToken = "r",
                ExpiresIn = 3600
            });

        var sut = new TwitchLibAuthClient(logger, transport);

        var result = await sut.ExchangeCodeAsync("cid", "secret", "code", "redirect");

        Assert.NotNull(result);
        Assert.Equal("a", result.AccessToken);
        Assert.Equal("r", result.RefreshToken);
        Assert.Equal(3600, result.ExpiresIn);
        await transport.Received(1).ExchangeCodeAsync("cid", "secret", "code", "redirect");
    }

    [Fact]
    public async Task ExchangeCodeAsync_ShouldRetryTransientFailures_AndSucceed()
    {
        var logger = Substitute.For<ILogger<TwitchLibAuthClient>>();
        var transport = Substitute.For<ITwitchAuthTransport>();
        var attempts = 0;

        transport.ExchangeCodeAsync("cid", "secret", "code", "redirect")
            .Returns(_ =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new HttpRequestException("network issue");
                }

                return new TwitchAuthTokenResponse
                {
                    AccessToken = "token",
                    RefreshToken = "refresh",
                    ExpiresIn = 1234
                };
            });

        var sut = new TwitchLibAuthClient(logger, transport);

        var result = await sut.ExchangeCodeAsync("cid", "secret", "code", "redirect");

        Assert.NotNull(result);
        Assert.Equal("token", result.AccessToken);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ShouldThrowImmediately_OnNonTransientFailure()
    {
        var logger = Substitute.For<ILogger<TwitchLibAuthClient>>();
        var transport = Substitute.For<ITwitchAuthTransport>();
        var attempts = 0;

        transport.ExchangeCodeAsync("cid", "secret", "code", "redirect")
            .Returns(_ =>
            {
                attempts++;
                return Task.FromException<TwitchAuthTokenResponse?>(new InvalidOperationException("bad state"));
            });

        var sut = new TwitchLibAuthClient(logger, transport);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExchangeCodeAsync("cid", "secret", "code", "redirect"));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task GetAuthenticatedUserAsync_ShouldReturnNull_WhenTransportReturnsNull()
    {
        var logger = Substitute.For<ILogger<TwitchLibAuthClient>>();
        var transport = Substitute.For<ITwitchAuthTransport>();
        transport.GetAuthenticatedUserAsync("cid", "access").Returns((TwitchAuthenticatedUser?)null);

        var sut = new TwitchLibAuthClient(logger, transport);

        var result = await sut.GetAuthenticatedUserAsync("cid", "access");

        Assert.Null(result);
        await transport.Received(1).GetAuthenticatedUserAsync("cid", "access");
    }

    [Fact]
    public async Task GetAuthenticatedUserAsync_ShouldRetryTransientFailures_AndSucceed()
    {
        var logger = Substitute.For<ILogger<TwitchLibAuthClient>>();
        var transport = Substitute.For<ITwitchAuthTransport>();
        var attempts = 0;

        transport.GetAuthenticatedUserAsync("cid", "access")
            .Returns(_ =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new TimeoutException("timeout");
                }

                return new TwitchAuthenticatedUser
                {
                    Id = "1",
                    Login = "penguin",
                    DisplayName = "Penguin",
                    ProfileImageUrl = "https://example/image.png"
                };
            });

        var sut = new TwitchLibAuthClient(logger, transport);

        var result = await sut.GetAuthenticatedUserAsync("cid", "access");

        Assert.NotNull(result);
        Assert.Equal("penguin", result.Login);
        Assert.Equal(2, attempts);
    }
}
