using System.Collections.Concurrent;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions;

public class StarshipShowEventHandlerTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("3600")]
    [InlineData(" 45 ")]
    [InlineData("%Seconds%")]
    [InlineData("$math(30 * 2)$")]
    public void Validate_AcceptsBoundedLiteralsAndRuntimeExpressions(string duration)
    {
        Assert.Null(new StarshipShowEventType().Validate(new() { [nameof(StarshipShowEventType.DurationSeconds)] = duration }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("3601")]
    [InlineData("1.5")]
    public void Validate_RejectsBlankAndInvalidLiterals(string? duration)
    {
        Assert.NotNull(new StarshipShowEventType().Validate(new() { [nameof(StarshipShowEventType.DurationSeconds)] = duration }));
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesVariablesAndShowsEventWidget()
    {
        var service = Substitute.For<IStarshipExpeditionService>();
        var handler = new StarshipShowEventHandler(service);
        var variables = new ConcurrentDictionary<string, string> { ["Seconds"] = "45" };

        await handler.ExecuteAsync(new StarshipShowEventType { DurationSeconds = "%Seconds%" }, variables);

        await service.Received(1).ShowEventWidgetAsync(45, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task ExecuteAsync_RejectsInvalidDurations(string duration)
    {
        var service = Substitute.For<IStarshipExpeditionService>();
        var handler = new StarshipShowEventHandler(service);

        await Assert.ThrowsAsync<SubActionHandlerException>(() =>
            handler.ExecuteAsync(new StarshipShowEventType { DurationSeconds = duration }, new ConcurrentDictionary<string, string>()));

        await service.DidNotReceive().ShowEventWidgetAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
