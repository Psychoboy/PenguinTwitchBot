using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Pages.Components;
using PenguinTwitchBot.Pages.Games;

namespace PenguinTwitchBot.Test.Pages.Components;

public class ChatGameAutomationPanelTests
{
    [Theory]
    [InlineData("defuse")]
    [InlineData("heist")]
    [InlineData("gamble")]
    [InlineData("slot")]
    [InlineData("roulette")]
    [InlineData("ffa")]
    public async Task Panel_ShowsOnlyTheSelectedChatGamesOutcomeEvents(string commandName)
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var actionService = Substitute.For<IActionManagementService>();
        actionService.GetAllActionsAsync().Returns(Task.FromResult(new List<ActionType>()));
        context.Services.AddSingleton(actionService);

        var game = GameDescriptorRegistry.FindByCommandName(commandName);
        Assert.NotNull(game);

        var expectedOutcomes = TriggerCatalog.AllTriggers
            .Where(item => item.Type == TriggerTypes.DefaultCommand &&
                string.Equals(item.DefaultCommandName, commandName, StringComparison.OrdinalIgnoreCase) &&
                game.GetOutcomeEvents(commandName).Contains(item.DefaultCommandEventType ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            .Select(item => item.DisplayName)
            .ToArray();

        var cut = context.Render<GameOutcomeAutomationPanel>(parameters => parameters
            .Add(component => component.DefaultCommandName, commandName)
            .Add(component => component.GameTitle, game.Title));

        var displayedOutcomes = cut.FindAll(".game-outcome-automation-panel .mud-typography-body1")
            .Select(element => element.TextContent.Trim())
            .ToArray();

        Assert.NotEmpty(expectedOutcomes);
        Assert.Equal(expectedOutcomes, displayedOutcomes);
    }
}