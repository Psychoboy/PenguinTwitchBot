using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Pages.Games;

namespace PenguinTwitchBot.Test.Components;

public class StarshipRewardProfileEditorTests
{
    [Fact]
    public async Task Editor_PointTypeSelectShowsNamesAndStoresSelectedCurrencyId()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Render<MudPopoverProvider>();
        var pointTypeId1 = Guid.CreateVersion7();
        var pointTypeId2 = Guid.CreateVersion7();
        var reward = new StarshipRewardEntry { PointTypeId = Guid.Empty, Minimum = 5, Maximum = 10 };
        var profile = new StarshipRewardProfile { Tiers = [new() { Name = "Common", Rewards = [reward] }] };
        var editor = context.Render<StarshipRewardProfileEditor>(parameters => parameters
            .Add(component => component.Profile, profile)
            .Add(component => component.PointTypes, new Dictionary<Guid, string> { [pointTypeId1] = "Credits", [pointTypeId2] = "Tokens" }));
        var select = editor.FindComponent<MudSelect<Guid>>();

        Assert.Equal("Select a point type", select.Instance.ToStringFunc!(Guid.Empty));
        Assert.Contains(editor.FindComponents<MudSelectItem<Guid>>(), item => item.Instance.Value == pointTypeId1);
        Assert.Contains(editor.FindComponents<MudSelectItem<Guid>>(), item => item.Instance.Value == pointTypeId2);
        await context.Renderer.Dispatcher.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(pointTypeId2));

        Assert.Equal(pointTypeId2, reward.PointTypeId);
        Assert.Equal("Tokens", select.Instance.ToStringFunc!(reward.PointTypeId));
    }

    [Fact]
    public async Task Editor_NewLimitsAreUnlimitedAndCanBeClearedOrDeleted()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Render<MudPopoverProvider>();
        var pointTypeId = Guid.CreateVersion7();
        var profile = new StarshipRewardProfile
        {
            Tiers = [new() { Name = "Common", Rewards = [new() { PointTypeId = pointTypeId }] }]
        };
        var editor = context.Render<StarshipRewardProfileEditor>(parameters => parameters
            .Add(component => component.Profile, profile)
            .Add(component => component.PointTypes, new Dictionary<Guid, string> { [pointTypeId] = "Credits" }));
        editor.FindAll("button").Single(button => button.TextContent.Trim() == "Add Point Limit").Click();
        var cap = Assert.Single(profile.Caps);
        Assert.Null(cap.MaximumPerEvent);
        Assert.Null(cap.MaximumUnbankedPerVoyage);
        var field = editor.FindComponents<MudNumericField<long?>>().Single(component => component.Instance.Label == "Maximum Per Event");
        Assert.Equal("Unlimited", field.Instance.Placeholder);
        field.Find("input").Change("25");
        Assert.Equal(25, cap.MaximumPerEvent);
        field.Find("input").Change("");
        Assert.Null(cap.MaximumPerEvent);
        editor.Find("button[aria-label='Remove limit']").Click();
        profile.ValidateCapsAndLoss("test");
        Assert.Empty(profile.Caps);
        Assert.Null(profile.GetCap(pointTypeId).MaximumPerEvent);
    }

    [Fact]
    public async Task Editor_RendersRelativeOddsAndAddsAndReordersTiers()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var profile = new StarshipRewardProfile
        {
            Name = "Recovery",
            Tiers = [new() { Name = "Common", Weight = 3 }, new() { Name = "Legendary", Weight = 1 }]
        };
        var rendered = context.Render<StarshipRewardProfileEditor>(parameters => parameters.Add(component => component.Profile, profile));

        Assert.Contains("Common (75%)", rendered.Markup);
        Assert.Contains("Legendary (25%)", rendered.Markup);
        rendered.FindAll("button").Single(button => button.TextContent.Contains("Add Rarity Tier", StringComparison.Ordinal)).Click();
        Assert.Equal(3, profile.Tiers.Count);
        rendered.FindAll("button[aria-label='Move tier up']")[1].Click();
        Assert.Equal("Legendary", profile.Tiers[0].Name);
    }
}