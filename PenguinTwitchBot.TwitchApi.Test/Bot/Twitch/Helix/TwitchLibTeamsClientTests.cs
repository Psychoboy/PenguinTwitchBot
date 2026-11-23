using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.Teams;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class TeamsClientTests
{
    [Fact]
    public async Task GetChannelTeamsAsync_SuccessfulCall_ReturnsResponse()
    {
        var logger = Substitute.For<ILogger<TeamsClient>>();
        var transport = Substitute.For<ITeamsTransport>();
        var expected = new GetChannelTeamsResponse([
            new ChannelTeam("t1", "penguins", "Penguins", "b1", "broadcaster", "Broadcaster")
        ]);
        transport.GetChannelTeamsAsync("client", "token", "b1").Returns(expected);

        var client = new TeamsClient(logger, transport);
        var result = await client.GetChannelTeamsAsync("client", "token", "b1");

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetChannelTeamsAsync_TransientError_RetriesAndSucceeds()
    {
        var logger = Substitute.For<ILogger<TeamsClient>>();
        var transport = Substitute.For<ITeamsTransport>();
        var expected = new GetChannelTeamsResponse([]);
        transport.GetChannelTeamsAsync("client", "token", "b1").Returns(
            Task.FromException<GetChannelTeamsResponse>(new TaskCanceledException("Transient")),
            Task.FromResult(expected));

        var client = new TeamsClient(logger, transport);
        var result = await client.GetChannelTeamsAsync("client", "token", "b1");

        Assert.Equal(expected, result);
        await transport.Received(2).GetChannelTeamsAsync("client", "token", "b1");
    }

    [Fact]
    public async Task GetTeamByIdAsync_SuccessfulCall_ReturnsResponse()
    {
        var logger = Substitute.For<ILogger<TeamsClient>>();
        var transport = Substitute.For<ITeamsTransport>();
        var expected = new GetTeamsResponse([
            new Team("t1", "penguins", "Penguins", [new TeamMember("u1", "alice", "Alice")])
        ]);
        transport.GetTeamByIdAsync("client", "token", "t1").Returns(expected);

        var client = new TeamsClient(logger, transport);
        var result = await client.GetTeamByIdAsync("client", "token", "t1");

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task TeamsTransport_GetChannelTeamsAsync_WhenDataIsNull_ReturnsEmptyList()
    {
        var handler = new StubHttpMessageHandler("""{"data":null}""");
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.twitch.tv/helix/")
        });

        var transport = new TeamsTransport(factory);
        var result = await transport.GetChannelTeamsAsync("client", "token", "b1");

        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task TeamsTransport_GetTeamByIdAsync_DeserializesMembers()
    {
        var json = """
        {
          "data": [
            {
              "id": "t1",
              "team_name": "penguins",
              "team_display_name": "Penguin Squad",
              "users": [
                { "user_id": "101", "user_login": "alice", "user_name": "Alice" },
                { "user_id": "102", "user_login": "bob", "user_name": "Bob" }
              ]
            }
          ]
        }
        """;
        var handler = new StubHttpMessageHandler(json);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.twitch.tv/helix/")
        });

        var transport = new TeamsTransport(factory);
        var result = await transport.GetTeamByIdAsync("client", "token", "t1");

        var team = Assert.Single(result.Data);
        Assert.Equal("Penguin Squad", team.TeamDisplayName);
        Assert.Equal(2, team.Users.Count);
        Assert.Equal("101", team.Users[0].UserId);
        Assert.Equal("alice", team.Users[0].UserLogin);
        Assert.Equal("Alice", team.Users[0].UserName);
    }

    private sealed class StubHttpMessageHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
