using System.Collections.Concurrent;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions;

public class StarshipExpeditionControlHandlerTests
{
    [Fact]
    public async Task StartUsesConfiguredSettingsAndTriggeringUser()
    {
        var service = Substitute.For<IStarshipExpeditionService>();
        service.StartExpeditionAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new StarshipExpeditionSnapshot());
        var handler = new StarshipStartExpeditionHandler(service);
        var variables = new ConcurrentDictionary<string, string> { ["User"] = "ChannelPointUser" };

        await handler.ExecuteAsync(new StarshipStartExpeditionType(), variables);

        await service.Received(1).StartExpeditionAsync("ChannelPointUser", null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopUsesTriggeringUserAndAllowsNoActiveVoyage()
    {
        var service = Substitute.For<IStarshipExpeditionService>();
        service.StopExpeditionAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns((StarshipExpeditionSnapshot?)null);
        var handler = new StarshipStopExpeditionHandler(service);
        var variables = new ConcurrentDictionary<string, string> { ["User"] = "Moderator" };

        await handler.ExecuteAsync(new StarshipStopExpeditionType(), variables);

        await service.Received(1).StopExpeditionAsync("Moderator", Arg.Any<CancellationToken>());
    }
}
