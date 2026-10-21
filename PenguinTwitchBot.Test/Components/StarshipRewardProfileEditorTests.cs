using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Pages.Games;

namespace PenguinTwitchBot.Test.Components;

public class StarshipRewardProfileEditorTests
{
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