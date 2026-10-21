using System.Collections.Concurrent;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class StarshipShowStatusHandlerTests
    {
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
