using PenguinTwitchBot.TwitchApi.Helix;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class UsersClientTests
{
    [Fact]
    public async Task GetUsersAsync_ShouldReturnResponse_WhenTransportSucceeds()
    {
        var logger = Substitute.For<ILogger<UsersClient>>();
        var transport = Substitute.For<IUsersTransport>();
        var response = CreateResponse();

        transport.GetUsersAsync("cid", "token", Arg.Any<List<string>?>(), Arg.Any<List<string>?>()).Returns(response);
        var sut = new UsersClient(logger, transport);

        var result = await sut.GetUsersAsync("cid", "token", null, ["penguin"]);

        Assert.Same(response, result);
        await transport.Received(1).GetUsersAsync("cid", "token", Arg.Any<List<string>?>(), Arg.Any<List<string>?>());
    }

    [Fact]
    public async Task GetUsersAsync_ShouldRetryTransientFailures_AndSucceed()
    {
        var logger = Substitute.For<ILogger<UsersClient>>();
        var transport = Substitute.For<IUsersTransport>();
        var attempts = 0;

        transport.GetUsersAsync("cid", "token", Arg.Any<List<string>?>(), Arg.Any<List<string>?>())
            .Returns(_ =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new HttpRequestException("network issue");
                }

                return CreateResponse();
            });

        var sut = new UsersClient(logger, transport);

        var result = await sut.GetUsersAsync("cid", "token", null, ["penguin"]);

        Assert.NotNull(result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task GetUsersAsync_ShouldThrowImmediately_OnNonTransientFailure()
    {
        var logger = Substitute.For<ILogger<UsersClient>>();
        var transport = Substitute.For<IUsersTransport>();
        var attempts = 0;

        transport.GetUsersAsync("cid", "token", Arg.Any<List<string>?>(), Arg.Any<List<string>?>())
            .Returns(_ =>
            {
                attempts++;
                return Task.FromException<GetUsersResponse>(new InvalidOperationException("bad state"));
            });

        var sut = new UsersClient(logger, transport);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetUsersAsync("cid", "token", null, ["penguin"]));
        Assert.Equal(1, attempts);
    }

    private static GetUsersResponse CreateResponse()
    {
        const string json = "{\"data\":[{\"id\":\"1\",\"login\":\"penguin\",\"display_name\":\"Penguin\",\"description\":\"\",\"profile_image_url\":\"https://example/img.png\",\"offline_image_url\":\"\",\"type\":\"\",\"broadcaster_type\":\"\",\"created_at\":\"2024-01-01T00:00:00Z\",\"email\":\"\"}]}";
        return Newtonsoft.Json.JsonConvert.DeserializeObject<GetUsersResponse>(json)!;
    }
}
