using System.Collections.Concurrent;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class StarshipShowStatusHandlerTests
    {
        [Theory]
        [InlineData("1")]
        [InlineData("3600")]
        [InlineData(" 45 ")]
        [InlineData("%Seconds%")]
        [InlineData("$math(30 * 2)$")]
        public void Validate_AcceptsBoundedLiteralsAndRuntimeExpressions(string duration)
        {
            Assert.Null(new StarshipShowStatusType().Validate(new() { [nameof(StarshipShowStatusType.DurationSeconds)] = duration }));
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
            Assert.NotNull(new StarshipShowStatusType().Validate(new() { [nameof(StarshipShowStatusType.DurationSeconds)] = duration }));
        }

        [Fact]
        public async Task ExecuteAsync_ResolvesVariablesAndShowsStatusWidget()
        {
            var service = Substitute.For<IStarshipExpeditionService>();
            var handler = new StarshipShowStatusHandler(service);
            var variables = new ConcurrentDictionary<string, string>();
            variables["Seconds"] = "45";

            await handler.ExecuteAsync(new StarshipShowStatusType { DurationSeconds = "%Seconds%" }, variables);

            await service.Received(1).ShowStatusWidgetAsync(45, Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("0")]
        [InlineData("-5")]
        public async Task ExecuteAsync_RejectsInvalidDurations(string duration)
        {
            var service = Substitute.For<IStarshipExpeditionService>();
            var handler = new StarshipShowStatusHandler(service);

            await Assert.ThrowsAsync<SubActionHandlerException>(() =>
                handler.ExecuteAsync(new StarshipShowStatusType { DurationSeconds = duration }, new ConcurrentDictionary<string, string>()));

            await service.DidNotReceive().ShowStatusWidgetAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }
}
