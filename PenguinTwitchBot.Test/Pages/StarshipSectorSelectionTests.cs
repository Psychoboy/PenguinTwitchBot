using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
using PenguinTwitchBot.Pages.Components;
using PenguinTwitchBot.Shared.Help;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Test.Pages;

public class StarshipSectorSelectionTests
{
    [Fact]
    public async Task SectorCheckboxes_UpdatePoolAndPreventSavingFewerThanTwoSectors()
    {
        var settings = new StarshipExpeditionSettings();
        var store = new Mock<IStarshipExpeditionSettingsStore>();
        await using var context = CreatePageContext(settings, store);
        var page = context.Render<StarshipExpeditionGame>();

        await page.WaitForAssertionAsync(() => Assert.Contains("5 available (minimum 2)", page.Markup));
        Assert.DoesNotContain(page.FindComponents<MudColorPicker>(), component => component.Instance.Label == "Panel colour");
        foreach (var sector in StarshipExpeditionSettings.BuiltInSectors.Skip(1))
        {
            await page.FindComponents<MudCheckBox<bool>>().Single(component => component.Instance.Label == sector)
                .Find("input").ChangeAsync(new ChangeEventArgs { Value = false });
            Assert.Contains(sector, settings.DisabledBuiltInSectors);
        }
        await page.WaitForAssertionAsync(() => Assert.Contains("1 available (minimum 2)", page.Markup));
        await page.FindAll("button").Single(button => button.TextContent.Contains("Save Settings", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        await page.WaitForAssertionAsync(() => Assert.Contains("Enable or add at least two expedition sectors.", page.Markup));
        store.Verify(instance => instance.SaveAsync(It.IsAny<StarshipExpeditionSettings>(), It.IsAny<CancellationToken>()), Times.Never);

        await page.FindComponents<MudCheckBox<bool>>().Single(component => component.Instance.Label == "Ternion Belt")
            .Find("input").ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.DoesNotContain("Ternion Belt", settings.DisabledBuiltInSectors);
        await page.FindAll("button").Single(button => button.TextContent.Contains("Save Settings", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        await page.WaitForAssertionAsync(() => store.Verify(instance => instance.SaveAsync(
            It.Is<StarshipExpeditionSettings>(value => value.GetEffectiveSectors().Count == 2), It.IsAny<CancellationToken>()), Times.Once));

        var editor = page.FindComponent<StarshipEventCatalogEditor>();
        await editor.FindAll("button").Single(button => button.TextContent.Trim() == "Add Event").ClickAsync(new MouseEventArgs());
        await editor.FindComponents<MudTextField<string>>().Single(component => component.Instance.Label == "Title")
            .Find("input").ChangeAsync(new ChangeEventArgs { Value = "Page Save Draft" });
        await page.InvokeAsync(() => page.FindComponent<MudTabs>().Instance.ActivatePanelAsync(0));
        Assert.Single(page.FindComponents<StarshipEventCatalogEditor>());
        await page.FindAll("button").Single(button => button.TextContent.Contains("Save Settings", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        await page.WaitForAssertionAsync(() => store.Verify(instance => instance.SaveAsync(
            It.Is<StarshipExpeditionSettings>(value => value.EventCatalog.Any(definition => definition.Title == "Page Save Draft")), It.IsAny<CancellationToken>()), Times.Exactly(2)));
        await page.InvokeAsync(() => page.FindComponent<MudTabs>().Instance.ActivatePanelAsync(4));
        await page.WaitForAssertionAsync(() => Assert.Contains(page.FindComponents<MudColorPicker>(), component => component.Instance.Label == "Panel colour"));
        await page.InvokeAsync(() => page.FindComponent<MudTabs>().Instance.ActivatePanelAsync(0));
        await page.WaitForAssertionAsync(() => Assert.DoesNotContain(page.FindComponents<MudColorPicker>(), component => component.Instance.Label == "Panel colour"));
        Assert.Single(page.FindComponents<StarshipEventCatalogEditor>());
    }

    [Fact]
    public async Task GameEnableSwitch_IsVisibleDefaultOffAndSavedWithPageSettings()
    {
        var settings = new StarshipExpeditionSettings();
        var store = new Mock<IStarshipExpeditionSettingsStore>();
        await using var context = CreatePageContext(settings, store);
        var page = context.Render<StarshipExpeditionGame>();
        var enable = page.FindComponents<MudSwitch<bool>>().Single(component => component.Instance.Label == "Enable Starship Expedition");

        Assert.False(enable.Find("input").HasAttribute("checked"));
        await enable.Find("input").ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.True(settings.GameEnabled);
        await page.FindAll("button").Single(button => button.TextContent.Contains("Save Settings", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());

        await page.WaitForAssertionAsync(() => store.Verify(instance => instance.SaveAsync(
            It.Is<StarshipExpeditionSettings>(value => value.GameEnabled), It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public async Task AudioTab_ExposesBoardingEventTypeAndEmergencySoundSettings()
    {
        var settings = new StarshipExpeditionSettings();
        var store = new Mock<IStarshipExpeditionSettingsStore>();
        await using var context = CreatePageContext(settings, store);
        var page = context.Render<StarshipExpeditionGame>();
        await page.InvokeAsync(() => page.FindComponent<MudTabs>().Instance.ActivatePanelAsync(5));
        await page.WaitForAssertionAsync(() =>
        {
            var titles = page.FindComponents<DropGameSoundEditor>().Select(component => component.Instance.Title).ToList();
            Assert.Contains("Crew Boarding Open Cue", titles);
            Assert.Contains("Choose a Route Default Cue", titles);
            Assert.Contains("Emergency Return Cue", titles);
        });
        Assert.Equal("/media/starship_boarding_open.wav", settings.BoardingOpenSound.AudioFile);
        Assert.Equal("/media/starship_emergency_return.wav", settings.EmergencyReturnSound.AudioFile);
    }

    [Fact]
    public async Task BalanceTest_UsesSandboxOverridesWithoutSavingPageSettings()
    {
        var settings = new StarshipExpeditionSettings();
        var store = new Mock<IStarshipExpeditionSettingsStore>();
        var expeditionService = new Mock<IStarshipExpeditionService>();
        expeditionService.Setup(service => service.BuildPreview(It.IsAny<StarshipExpeditionSettings>(), It.IsAny<int>()))
            .Returns(Array.Empty<StarshipExpeditionEventSnapshot>());
        await using var context = CreatePageContext(settings, store, expeditionService);
        var page = context.Render<StarshipExpeditionGame>();
        var testTab = page.FindComponents<MudTabPanel>().Single(panel => panel.Instance.Text == "Test");

        await testTab.FindComponents<MudNumericField<int>>().Single(field => field.Instance.Label == "Maximum Hull")
            .Find("input").ChangeAsync(new ChangeEventArgs { Value = "70" });
        await testTab.FindComponents<MudNumericField<int>>().Single(field => field.Instance.Label == "Starting Hull")
            .Find("input").ChangeAsync(new ChangeEventArgs { Value = "65" });
        await testTab.FindComponents<MudNumericField<int>>().Single(field => field.Instance.Label == "Travel Supplies per Leg")
            .Find("input").ChangeAsync(new ChangeEventArgs { Value = "8" });
        await testTab.FindComponents<MudNumericField<int>>().Single(field => field.Instance.Label == "Runs")
            .Find("input").ChangeAsync(new ChangeEventArgs { Value = "20" });
        await testTab.FindAll("button").Single(button => button.TextContent.Contains("Run Balance Test", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());

        await page.WaitForAssertionAsync(() => expeditionService.Verify(service => service.BuildPreview(
            It.Is<StarshipExpeditionSettings>(value => value.ShipSystems.MaximumHull == 70 &&
                value.ShipSystems.StartingHull == 65 && value.ShipSystems.TravelSuppliesCost == 8), It.IsAny<int>()), Times.Exactly(20)));
        Assert.Equal(80, settings.ShipSystems.MaximumHull);
        Assert.Equal(80, settings.ShipSystems.StartingHull);
        store.Verify(service => service.SaveAsync(It.IsAny<StarshipExpeditionSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveRewardProfile_ClearsCatalogReferencesCaseInsensitivelyWithImmutableUpdates()
    {
        var settings = new StarshipExpeditionSettings();
        var profile = new StarshipRewardProfile { Id = "review-profile", Name = "Review Profile", Tiers = [new() { Name = "Common" }] };
        settings.RewardProfiles.Add(profile);
        settings.EventRewardProfileIds["SignalDetected"] = profile.Id;
        var index = settings.EventCatalog.FindIndex(definition => definition.EventType == "SignalDetected");
        settings.EventCatalog[index] = settings.EventCatalog[index] with { RewardProfileId = profile.Id };
        await using var context = CreatePageContext(settings, new Mock<IStarshipExpeditionSettingsStore>());
        var page = context.Render<StarshipExpeditionGame>();
        await page.WaitForAssertionAsync(() => Assert.Contains("5 available (minimum 2)", page.Markup));
        var original = settings.EventCatalog[index] with { RewardProfileId = "REVIEW-PROFILE" };
        settings.EventCatalog[index] = original;
        var panel = page.FindComponents<MudExpansionPanel>().Single(component => component.Instance.Text?.StartsWith("Review Profile", StringComparison.Ordinal) == true);

        await panel.FindAll("button").Single(button => button.TextContent.Trim() == "Delete Profile").ClickAsync(new MouseEventArgs());

        Assert.DoesNotContain(profile, settings.RewardProfiles);
        Assert.Equal(string.Empty, settings.EventRewardProfileIds["SignalDetected"]);
        Assert.Equal(original with { RewardProfileId = string.Empty }, settings.EventCatalog[index]);
        Assert.Equal("REVIEW-PROFILE", original.RewardProfileId);
    }

    private static BunitContext CreatePageContext(StarshipExpeditionSettings settings, Mock<IStarshipExpeditionSettingsStore> store,
        Mock<IStarshipExpeditionService>? expeditionService = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule("./js/starshipEditor.js");
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton(new MediaUploadService(Path.GetTempPath(), NullLogger<MediaUploadService>.Instance));
        context.ComponentFactories.AddStub<HelpButton>();
        context.ComponentFactories.AddStub<PenguinTwitchBot.Shared.MediaUrlPicker>();
        context.ComponentFactories.AddStub<PenguinTwitchBot.Pages.Components.GameOutcomeAutomationPanel>();
        context.ComponentFactories.AddStub<StarshipRewardProfileEditor>();
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
        context.Services.AddSingleton(expeditionService?.Object ?? Mock.Of<IStarshipExpeditionService>());
        context.Render<MudPopoverProvider>();
        return context;
    }
}