using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Pages.Games;
using PenguinTwitchBot.Shared.Help;

namespace PenguinTwitchBot.Test.Pages;

public class StarshipSectorSelectionTests
{
    [Fact]
    public async Task SectorCheckboxes_UpdatePoolAndPreventSavingFewerThanTwoSectors()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule("./js/starshipEditor.js");
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.ComponentFactories.AddStub<HelpButton>();
        context.ComponentFactories.AddStub<PenguinTwitchBot.Shared.MediaUrlPicker>();
        context.ComponentFactories.AddStub<PenguinTwitchBot.Pages.Components.GameOutcomeAutomationPanel>();
        var settings = new StarshipExpeditionSettings();
        var store = new Mock<IStarshipExpeditionSettingsStore>();
        store.Setup(instance => instance.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        store.Setup(instance => instance.SaveAsync(It.IsAny<StarshipExpeditionSettings>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var points = new Mock<IPointsSystem>();
        points.Setup(instance => instance.GetPointTypes()).ReturnsAsync(new List<PointType>());
        var actions = new Mock<IActionManagementService>();
        actions.Setup(instance => instance.GetAllActionsAsync()).ReturnsAsync(new List<ActionType>());
        var features = new Mock<IFeatureRuntimeCoordinator>();
        features.Setup(instance => instance.IsEnabled(It.IsAny<string>())).Returns(true);
        context.Services.AddSingleton(store.Object);
        context.Services.AddSingleton(points.Object);
        context.Services.AddSingleton(actions.Object);
        context.Services.AddSingleton(features.Object);
        context.Services.AddSingleton(Mock.Of<IStarshipExpeditionService>());
        context.Render<MudPopoverProvider>();
        var page = context.Render<StarshipExpeditionGame>();

        page.WaitForAssertion(() => Assert.Contains("5 available (minimum 2)", page.Markup));
        foreach (var sector in StarshipExpeditionSettings.BuiltInSectors.Skip(1))
            page.FindComponents<MudCheckBox<bool>>().Single(component => component.Instance.Label == sector).Find("input").Change(false);
        page.WaitForAssertion(() => Assert.Contains("1 available (minimum 2)", page.Markup));
        page.FindAll("button").Single(button => button.TextContent.Contains("Save Settings", StringComparison.Ordinal)).Click();
        page.WaitForAssertion(() => Assert.Contains("Enable or add at least two expedition sectors.", page.Markup));
        store.Verify(instance => instance.SaveAsync(It.IsAny<StarshipExpeditionSettings>(), It.IsAny<CancellationToken>()), Times.Never);

        page.FindComponents<MudCheckBox<bool>>().Single(component => component.Instance.Label == "Ternion Belt").Find("input").Change(true);
        page.FindAll("button").Single(button => button.TextContent.Contains("Save Settings", StringComparison.Ordinal)).Click();
        page.WaitForAssertion(() => store.Verify(instance => instance.SaveAsync(
            It.Is<StarshipExpeditionSettings>(value => value.GetEffectiveSectors().Count == 2), It.IsAny<CancellationToken>()), Times.Once));

        var editor = page.FindComponent<StarshipEventCatalogEditor>();
        editor.FindAll("button").Single(button => button.TextContent.Trim() == "Add Event").Click();
        editor.FindComponents<MudTextField<string>>().Single(component => component.Instance.Label == "Title").Find("input").Change("Page Save Draft");
        await page.InvokeAsync(() => page.FindComponent<MudTabs>().Instance.ActivatePanelAsync(0));
        Assert.Single(page.FindComponents<StarshipEventCatalogEditor>());
        page.FindAll("button").Single(button => button.TextContent.Contains("Save Settings", StringComparison.Ordinal)).Click();
        page.WaitForAssertion(() => store.Verify(instance => instance.SaveAsync(
            It.Is<StarshipExpeditionSettings>(value => value.EventCatalog.Any(definition => definition.Title == "Page Save Draft")), It.IsAny<CancellationToken>()), Times.Exactly(2)));
    }
}