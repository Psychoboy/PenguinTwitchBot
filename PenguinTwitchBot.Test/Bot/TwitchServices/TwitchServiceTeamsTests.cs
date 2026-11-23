using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.TwitchApi.Auth;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.Streams;
using PenguinTwitchBot.TwitchApi.Models.Teams;
using PenguinTwitchBot.TwitchApi.Models.Users;
using StreamModel = PenguinTwitchBot.TwitchApi.Models.Streams.Stream;

namespace PenguinTwitchBot.Test.Bot.TwitchServices;

public class TwitchServiceTeamsTests
{
    private readonly ILogger<TwitchService> _logger = Substitute.For<ILogger<TwitchService>>();
    private readonly IConfiguration _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["twitchAccessToken"] = "access-token",
        ["twitchRefreshToken"] = "refresh-token",
        ["twitchClientId"] = "client-id",
        ["twitchClientSecret"] = "client-secret",
        ["broadcaster"] = "broadcaster"
    }).Build();
    private readonly IStreamsClient _streamsClient = Substitute.For<IStreamsClient>();
    private readonly IUsersClient _usersClient = Substitute.For<IUsersClient>();
    private readonly ITeamsClient _teamsClient = Substitute.For<ITeamsClient>();

    private TwitchService CreateSut()
    {
        var settingsManager = new SettingsFileManager(Substitute.For<ILogger<SettingsFileManager>>(), _config);
        return new TwitchService(
            _logger,
            _config,
            Substitute.For<IAuthClient>(),
            Substitute.For<IChatClient>(),
            Substitute.For<IChannelPointsClient>(),
            Substitute.For<IModerationClient>(),
            Substitute.For<IChannelsClient>(),
            _streamsClient,
            Substitute.For<IClipsClient>(),
            Substitute.For<IGamesClient>(),
            Substitute.For<ISubscriptionsClient>(),
            Substitute.For<IRaidsClient>(),
            _usersClient,
            Substitute.For<IScheduleClient>(),
            _teamsClient,
            settingsManager,
            Substitute.For<IPenguinDispatcher>(),
            Substitute.For<IChatMessageIdTracker>());
    }

    [Fact]
    public async Task GetStreamTeamMembersAsync_ExcludesBroadcasterAndDeduplicatesAcrossTeams()
    {
        _usersClient.GetUsersAsync("client-id", "access-token", Arg.Any<List<string>?>(), Arg.Any<List<string>?>())
            .Returns(new GetUsersResponse([new User("b1", "broadcaster", "Broadcaster", "", default)]));

        _teamsClient.GetChannelTeamsAsync("client-id", "access-token", "b1")
            .Returns(new GetChannelTeamsResponse([
                new ChannelTeam("t1", "team1", "Team One", "b1", "broadcaster", "Broadcaster"),
                new ChannelTeam("t2", "team2", "Team Two", "b1", "broadcaster", "Broadcaster")
            ]));

        _teamsClient.GetTeamByIdAsync("client-id", "access-token", "t1")
            .Returns(new GetTeamsResponse([
                new Team("t1", "team1", "Team One", [
                    new TeamMember("b1", "broadcaster", "Broadcaster"),
                    new TeamMember("u1", "alice", "Alice"),
                    new TeamMember("u2", "bob", "Bob")
                ])
            ]));

        _teamsClient.GetTeamByIdAsync("client-id", "access-token", "t2")
            .Returns(new GetTeamsResponse([
                new Team("t2", "team2", "Team Two", [
                    new TeamMember("b1", "broadcaster", "Broadcaster"),
                    new TeamMember("u1", "alice", "Alice")
                ])
            ]));

        var sut = CreateSut();
        var members = await sut.GetStreamTeamMembersAsync();

        Assert.Equal(2, members.Count);
        Assert.DoesNotContain(members, m => m.UserId == "b1");

        var alice = Assert.Single(members, m => m.UserId == "u1");
        Assert.Equal(["Team One", "Team Two"], alice.TeamNames);

        var bob = Assert.Single(members, m => m.UserId == "u2");
        Assert.Equal(["Team One"], bob.TeamNames);
    }

    [Fact]
    public async Task GetStreamTeamMembersAsync_WhenNotInAnyTeam_ReturnsEmpty()
    {
        _usersClient.GetUsersAsync("client-id", "access-token", Arg.Any<List<string>?>(), Arg.Any<List<string>?>())
            .Returns(new GetUsersResponse([new User("b1", "broadcaster", "Broadcaster", "", default)]));

        _teamsClient.GetChannelTeamsAsync("client-id", "access-token", "b1")
            .Returns(new GetChannelTeamsResponse([]));

        var sut = CreateSut();
        var members = await sut.GetStreamTeamMembersAsync();

        Assert.Empty(members);
        await _teamsClient.DidNotReceive().GetTeamByIdAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task AreStreamsOnline_ChunksMoreThan100UserIds()
    {
        var userIds = Enumerable.Range(1, 150).Select(i => i.ToString()).ToList();

        _streamsClient.GetStreamsAsync("client-id", "access-token", Arg.Any<List<string>?>())
            .Returns(callInfo =>
            {
                var ids = callInfo.Arg<List<string>?>() ?? [];
                var firstId = ids[0];
                return new GetStreamsResponse([
                    new StreamModel(
                        Id: $"s-{firstId}",
                        UserId: firstId,
                        UserLogin: $"user{firstId}",
                        UserName: $"User{firstId}",
                        GameId: "g1",
                        GameName: "Just Chatting",
                        Type: "live",
                        Title: "Live",
                        Tags: [],
                        ViewerCount: 10,
                        StartedAt: DateTime.UtcNow,
                        Language: "en",
                        ThumbnailUrl: "",
                        IsMature: false)
                ]);
            });

        var sut = CreateSut();
        var online = await sut.AreStreamsOnline(userIds);

        Assert.Equal(2, online.Count);
        Assert.Equal("user1", online[0].UserLogin);
        Assert.Equal("user101", online[1].UserLogin);
        await _streamsClient.Received(2).GetStreamsAsync("client-id", "access-token", Arg.Any<List<string>?>());
    }
}
