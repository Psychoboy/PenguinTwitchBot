using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Bot.Commands.Games.UfoClaw;
using PenguinTwitchBot.Bot.Commands.WheelSpin;
using PenguinTwitchBot.Database.Bot.Models.Wheel;
using PenguinTwitchBot.Pages.Components;

namespace PenguinTwitchBot.Test.Pages.Components;

public class PrizeLabelPickerTests
{
    private static BunitContext CreateContext(IWheelService? wheels = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        if (wheels != null) context.Services.AddSingleton(wheels);
        return context;
    }

    private static IWheelService WheelsWith(params string[] labels)
    {
        var wheels = Substitute.For<IWheelService>();
        var wheel = new Wheel { Name = "Test" };
        wheel.Properties.AddRange(labels.Select(label => new WheelProperty { Label = label }));
        wheels.GetWheels().Returns(Task.FromResult(new List<Wheel> { wheel }));
        return wheels;
    }

    [Fact]
    public async Task WheelSpinResult_OffersDistinctSavedSliceLabels()
    {
        await using var context = CreateContext(WheelsWith("500 Points", "500 points", "Jackpot"));
        var cut = context.Render<PrizeLabelPicker>(parameters => parameters
            .Add(component => component.EventType, DefaultCommandEventTypes.WheelSpinResult));

        var autocomplete = cut.FindComponent<MudAutocomplete<string>>().Instance;
        Func<string, CancellationToken, Task<IEnumerable<string>>> search = autocomplete.SearchFunc!;
        var results = (await search(string.Empty, CancellationToken.None)).ToList();

        Assert.Equal(["500 Points", "Jackpot"], results);
    }

    [Fact]
    public async Task UnknownLabel_ShowsWarningButStillAcceptsTypedValue()
    {
        await using var context = CreateContext(WheelsWith("Jackpot"));
        string? chosen = null;
        var cut = context.Render<PrizeLabelPicker>(parameters => parameters
            .Add(component => component.EventType, DefaultCommandEventTypes.WheelSpinResult)
            .Add(component => component.Value, "Jackpott")
            .Add(component => component.ValueChanged, (string? value) => { chosen = value; }));

        Assert.Contains("No saved prize has this label", cut.Markup);
        await cut.InvokeAsync(() => cut.FindComponent<MudAutocomplete<string>>().Instance.ValueChanged.InvokeAsync("Jackpot"));
        Assert.Equal("Jackpot", chosen);
    }

    [Fact]
    public async Task MissingServices_FallBackToFreeText()
    {
        await using var context = CreateContext();
        var cut = context.Render<PrizeLabelPicker>(parameters => parameters
            .Add(component => component.EventType, DefaultCommandEventTypes.UfoClawResult)
            .Add(component => component.Value, "Anything"));

        Assert.DoesNotContain("No saved prize has this label", cut.Markup);
    }
}
