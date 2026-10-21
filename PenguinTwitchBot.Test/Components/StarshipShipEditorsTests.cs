using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Pages.Games;

namespace PenguinTwitchBot.Test.Components;

public class StarshipShipEditorsTests
{
    [Fact]
    public async Task ShipSettings_ExposeBoundedRulesAndCanDisableControls()
    {
        await using var context = CreateContext();
        var settings = new StarshipShipSystemsSettings();
        var editor = context.Render<StarshipShipSettingsEditor>(parameters => parameters.Add(component => component.Settings, settings));
        var hull = editor.FindComponents<MudNumericField<int>>().Single(component => component.Instance.Label == "Starting Hull");
        await hull.Find("input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "70" });
        Assert.Equal(70, settings.StartingHull);
        Assert.Equal(1, hull.Instance.Min);
        Assert.Equal(100, hull.Instance.Max);
        var enabled = editor.FindComponent<MudSwitch<bool>>();
        await editor.InvokeAsync(() => enabled.Instance.ValueChanged.InvokeAsync(false));
        Assert.False(settings.Enabled);
        Assert.Empty(editor.FindComponents<MudNumericField<int>>());
    }

    [Fact]
    public async Task ShipEffect_UsesImmutableCallbacksAndKeepsOutcomeCostsHidden()
    {
        await using var context = CreateContext();
        var original = new StarshipShipEffect { HullDelta = -10 };
        StarshipShipEffect? changed = null;
        var editor = context.Render<StarshipShipEffectEditor>(parameters => parameters
            .Add(component => component.Value, original)
            .Add(component => component.ValueChanged, value => changed = value));
        Assert.Equal(2, editor.FindComponents<MudNumericField<int>>().Count);
        var hull = editor.FindComponents<MudNumericField<int>>().Single(component => component.Instance.Label == "Ship Effect Hull Delta");
        await hull.Find("input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "-20" });
        Assert.Equal(-20, changed!.HullDelta);
        Assert.Equal(-10, original.HullDelta);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return context;
    }
}