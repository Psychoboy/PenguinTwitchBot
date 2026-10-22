using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Commands.Games.Balloon;
using PenguinTwitchBot.Bot.Commands.Games.Curling;
using PenguinTwitchBot.Bot.Commands.Games.Golf;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Commands;

namespace PenguinTwitchBot.Test.Bot.Commands;

public class BaseCommandRunHandlerTests
{
    [Theory]
    [InlineData("ParachuteDrop", "drop")]
    [InlineData("PlinkoDrop", "plinko")]
    [InlineData("BalloonAscent", "balloon")]
    [InlineData("Curling", "curling")]
    [InlineData("Golf", "golf")]
    [InlineData("StarshipExpedition", "starship")]
    public async Task DisabledGameCommand_IsLoggedAndNotQueued(string moduleName, string commandName)
    {
        var commandHandler = new Mock<ICommandHandler>();
        var properties = new DefaultCommand
        {
            CommandName = commandName,
            CustomCommandName = commandName,
            ModuleName = moduleName
        };
        commandHandler.Setup(handler => handler.GetCommand(commandName))
            .Returns(new Command(properties, Mock.Of<IBaseCommandService>()));
        var settings = new Mock<IDropGamesSettingsService>();
        settings.Setup(service => service.GetSettingsAsync()).ReturnsAsync(new DropGamesSettings());
        var parachute = new Mock<IParachuteDropService>();
        var plinko = new Mock<IPlinkoDropService>();
        var balloon = new Mock<IBalloonAscentService>();
        var curling = new Mock<ICurlingGameService>();
        var golf = new Mock<IGolfGameService>();
        var starship = new Mock<IStarshipExpeditionService>();
        starship.Setup(service => service.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync((StarshipExpeditionSnapshot?)null);
        var starshipSettings = new Mock<IStarshipExpeditionSettingsStore>();
        starshipSettings.Setup(store => store.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new StarshipExpeditionSettings());
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var handler = new BaseCommandRunHandler(commandHandler.Object, scopeFactory.Object,
            settings.Object, parachute.Object, plinko.Object, balloon.Object, curling.Object, golf.Object,
            starship.Object, starshipSettings.Object, NullLogger<BaseCommandRunHandler>.Instance);

        await handler.Handle(new RunCommandNotification
        {
            EventArgs = new CommandEventArgs { Command = commandName, Args = ["start"] }
        }, CancellationToken.None);

        scopeFactory.Verify(factory => factory.CreateScope(), Times.Never);
    }
}