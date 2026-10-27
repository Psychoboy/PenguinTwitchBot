using System.Collections.Concurrent;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Balloon;
using PenguinTwitchBot.Bot.Commands.Games.Curling;
using PenguinTwitchBot.Bot.Commands.Games.Golf;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Commands.Games.Racing;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions;

public class MinigameSessionSubActionHandlerTests
{
    [Fact]
    public async Task ParachuteStartAndStopHandlers_InvokeParachuteService()
    {
        var service = Substitute.For<IParachuteDropService>();
        var startHandler = new ParachuteStartSessionHandler(service);
        var stopHandler = new ParachuteStopSessionHandler(service);
        var vars = new ConcurrentDictionary<string, string> { ["User"] = "Tester" };

        await startHandler.ExecuteAsync(new ParachuteStartSessionType
        {
            DurationSeconds = "90",
            ThemeOverrideId = "winter"
        }, vars);

        await service.Received(1).StartSessionAsync(90, "winter", "Tester");

        await stopHandler.ExecuteAsync(new ParachuteStopSessionType { ClearQueue = true }, vars);

        await service.Received(1).ClearSessionQueueAsync();
        await service.Received(1).StopSessionAsync("Tester");
    }

    [Fact]
    public async Task PlinkoStartAndStopHandlers_InvokePlinkoService()
    {
        var service = Substitute.For<IPlinkoDropService>();
        var startHandler = new PlinkoStartSessionHandler(service);
        var stopHandler = new PlinkoStopSessionHandler(service);
        var vars = new ConcurrentDictionary<string, string> { ["User"] = "Tester" };

        await startHandler.ExecuteAsync(new PlinkoStartSessionType
        {
            DurationSeconds = "60",
            ThemeOverrideId = "halloween"
        }, vars);

        await service.Received(1).StartSessionAsync(60, "halloween", "Tester");

        await stopHandler.ExecuteAsync(new PlinkoStopSessionType { ClearQueue = false }, vars);

        await service.DidNotReceive().ClearSessionQueueAsync();
        await service.Received(1).StopSessionAsync("Tester");
    }

    [Fact]
    public async Task BalloonStartAndStopHandlers_InvokeBalloonService()
    {
        var service = Substitute.For<IBalloonAscentService>();
        var startHandler = new BalloonStartSessionHandler(service);
        var stopHandler = new BalloonStopSessionHandler(service);
        var vars = new ConcurrentDictionary<string, string> { ["User"] = "Tester" };

        await startHandler.ExecuteAsync(new BalloonStartSessionType
        {
            DurationSeconds = "75",
            ThemeOverrideId = "spring"
        }, vars);

        await service.Received(1).StartSessionAsync(75, "spring", "Tester");

        await stopHandler.ExecuteAsync(new BalloonStopSessionType { ClearQueue = true }, vars);

        await service.Received(1).ClearSessionQueueAsync();
        await service.Received(1).StopSessionAsync("Tester");
    }

    [Fact]
    public async Task CurlingStartAndStopHandlers_InvokeCurlingService()
    {
        var service = Substitute.For<ICurlingGameService>();
        var startHandler = new CurlingStartSessionHandler(service);
        var stopHandler = new CurlingStopSessionHandler(service);
        var vars = new ConcurrentDictionary<string, string> { ["User"] = "Tester" };

        await startHandler.ExecuteAsync(new CurlingStartSessionType
        {
            DurationSeconds = "120",
            ThemeOverrideId = "christmas"
        }, vars);

        await service.Received(1).StartSessionAsync(120, "christmas", "Tester");

        await stopHandler.ExecuteAsync(new CurlingStopSessionType { ClearQueue = true }, vars);

        await service.Received(1).ClearSessionQueueAsync();
        await service.Received(1).StopSessionAsync("Tester");
    }

    [Fact]
    public async Task GolfStartAndStopHandlers_InvokeGolfService()
    {
        var service = Substitute.For<IGolfGameService>();
        var startHandler = new GolfStartSessionHandler(service);
        var stopHandler = new GolfStopSessionHandler(service);
        var vars = new ConcurrentDictionary<string, string> { ["User"] = "Tester" };

        await startHandler.ExecuteAsync(new GolfStartSessionType
        {
            DurationSeconds = "45",
            ThemeOverrideId = "summer"
        }, vars);

        await service.Received(1).StartSessionAsync(45, "summer", "Tester");

        await stopHandler.ExecuteAsync(new GolfStopSessionType { ClearQueue = true }, vars);

        await service.Received(1).ClearSessionQueueAsync();
        await service.Received(1).StopSessionAsync("Tester");
    }

    [Fact]
    public async Task RacingStartAndStopHandlers_InvokeRacingServiceWithOverrides()
    {
        var service = Substitute.For<IRacingGameService>();
        var startHandler = new RacingStartSessionHandler(service);
        var stopHandler = new RacingStopSessionHandler(service);
        var vars = new ConcurrentDictionary<string, string> { ["User"] = "RacerHost" };

        await startHandler.ExecuteAsync(new RacingStartSessionType
        {
            GameMode = "DestructionDerby",
            TrackType = "DerbyBowl",
            TotalLaps = "4",
            DurationSeconds = "40",
            ThemeOverrideId = "fall"
        }, vars);

        await service.Received(1).StartRaceLobbyAsync(
            40,
            "fall",
            "RacerHost",
            RacingGameMode.DestructionDerby,
            RacingTrackType.DerbyBowl,
            4);

        await stopHandler.ExecuteAsync(new RacingStopSessionType { ClearQueue = true }, vars);

        await service.Received(1).ClearSessionQueueAsync();
        await service.Received(1).StopSessionAsync("RacerHost");
    }

    [Fact]
    public async Task RacingJoinHandler_InvokesRacingServiceForLobbyAndSolo()
    {
        var service = Substitute.For<IRacingGameService>();
        var handler = new RacingJoinHandler(service);
        var vars = new ConcurrentDictionary<string, string>
        {
            ["User"] = "SpeedyPenguin",
            ["UserId"] = "u-123",
            ["DisplayName"] = "SpeedyPenguin",
            ["Color"] = "#FF5500"
        };

        await handler.ExecuteAsync(new RacingJoinType
        {
            Username = "%User%",
            EntryMode = "Solo",
            EmoteOrImage = "PogChamp",
            ThemeOverrideId = "winter"
        }, vars);

        await service.Received(1).TriggerSoloTimeTrialAsync(
            Arg.Is<CommandEventArgs>(e => e.Name == "speedypenguin"),
            "PogChamp",
            "winter",
            true);

        await handler.ExecuteAsync(new RacingJoinType
        {
            Username = "%User%",
            EntryMode = "Lobby",
            EmoteOrImage = "Kappa",
            ThemeOverrideId = "summer"
        }, vars);

        await service.Received(1).TriggerJoinRaceAsync(
            Arg.Is<CommandEventArgs>(e => e.Name == "speedypenguin"),
            "Kappa",
            "summer",
            true);
    }
}

