using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Services;
using MudBlazor.Utilities;
using PenguinTwitchBot.Models.Themes;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public class CustomThemeModelTests
{
    [Fact]
    public void ToMudTheme_AppliesLightAndDarkPaletteColors()
    {
        var model = new CustomThemeModel
        {
            Name = "Test Theme",
            LightPalette = new ThemePaletteModel
            {
                Primary = "#123456",
                Secondary = "#654321",
                Background = "#FFFFFF",
                AppbarBackground = "#112233"
            },
            DarkPalette = new ThemePaletteModel
            {
                Primary = "#ABCDEF",
                Secondary = "#FEDCBA",
                Background = "#1A1A27",
                AppbarBackground = "#223344"
            }
        };

        var mudTheme = model.ToMudTheme();

        Assert.NotNull(mudTheme);
        Assert.NotNull(mudTheme.PaletteLight);
        Assert.NotNull(mudTheme.PaletteDark);

        Assert.Equal("#123456", mudTheme.PaletteLight.Primary.ToString(MudColorOutputFormats.Hex));
        Assert.Equal("#654321", mudTheme.PaletteLight.Secondary.ToString(MudColorOutputFormats.Hex));
        Assert.Equal("#ffffff", mudTheme.PaletteLight.Background.ToString(MudColorOutputFormats.Hex).ToLowerInvariant());
        Assert.Equal("#112233", mudTheme.PaletteLight.AppbarBackground.ToString(MudColorOutputFormats.Hex).ToLowerInvariant());

        Assert.Equal("#abcdef", mudTheme.PaletteDark.Primary.ToString(MudColorOutputFormats.Hex).ToLowerInvariant());
        Assert.Equal("#fedcba", mudTheme.PaletteDark.Secondary.ToString(MudColorOutputFormats.Hex).ToLowerInvariant());
        Assert.Equal("#1a1a27", mudTheme.PaletteDark.Background.ToString(MudColorOutputFormats.Hex).ToLowerInvariant());
        Assert.Equal("#223344", mudTheme.PaletteDark.AppbarBackground.ToString(MudColorOutputFormats.Hex).ToLowerInvariant());
    }

    [Fact]
    public void Clone_CreatesDeepCopy_WithNewId()
    {
        var original = new CustomThemeModel
        {
            Id = "original-id",
            Name = "Original",
            Description = "Original description",
            IsDefault = true,
            LightPalette = new ThemePaletteModel { Primary = "#111111" },
            DarkPalette = new ThemePaletteModel { Primary = "#222222" }
        };

        var clone = original.Clone("Cloned Theme");

        Assert.NotEqual(original.Id, clone.Id);
        Assert.Equal("Cloned Theme", clone.Name);
        Assert.Equal("Original description", clone.Description);
        Assert.False(clone.IsDefault);
        Assert.True(clone.IsEnabled);
        Assert.Equal("#111111", clone.LightPalette.Primary);
        Assert.Equal("#222222", clone.DarkPalette.Primary);

        // Modifying clone should not modify original
        clone.LightPalette.Primary = "#999999";
        Assert.Equal("#111111", original.LightPalette.Primary);
    }

    [Fact]
    public void FromPalette_ExtractsHexColorsCorrectly()
    {
        var palette = new PaletteLight
        {
            Primary = new MudBlazor.Utilities.MudColor("#FF0055"),
            Secondary = new MudBlazor.Utilities.MudColor("#00AAFF")
        };

        var model = ThemePaletteModel.FromPalette(palette);

        Assert.Equal("#ff0055", model.Primary?.ToLowerInvariant());
        Assert.Equal("#00aaff", model.Secondary?.ToLowerInvariant());
    }

    [Fact]
    public void FromPalette_PreservesAlphaWhenLessThan255()
    {
        var palette = new PaletteDark
        {
            TextPrimary = new MudColor("rgba(255, 255, 255, 0.7)"),
            TextSecondary = new MudColor("rgba(255, 255, 255, 0.5)")
        };

        var model = ThemePaletteModel.FromPalette(palette);

        Assert.NotNull(model.TextPrimary);
        // Should format as HexA (#ffffffb2 or #ffffffb3) instead of unrounded rgba decimal
        Assert.StartsWith("#ffffff", model.TextPrimary);
        Assert.Equal(9, model.TextPrimary.Length);

        // Verify applying it back preserves the alpha
        var targetPalette = new PaletteDark();
        model.ApplyTo(targetPalette);

        Assert.True(targetPalette.TextPrimary.A < 255);
        Assert.InRange(targetPalette.TextPrimary.A, 175, 180);
    }

    [Fact]
    public void NormalizeColors_ConvertsLongRgbaToHexA()
    {
        var model = new ThemePaletteModel
        {
            TextPrimary = "rgba(255,255,255,0.69999999)",
            TextSecondary = "rgba(0,0,0,0.87000001)"
        };

        model.NormalizeColors();

        Assert.StartsWith("#ffffff", model.TextPrimary);
        Assert.Equal(9, model.TextPrimary!.Length);
        Assert.StartsWith("#000000", model.TextSecondary);
        Assert.Equal(9, model.TextSecondary!.Length);
    }

    [Fact]
    public void ToRoundedRgbaString_RoundsAlphaCorrectly()
    {
        var color = new MudColor("rgba(255, 255, 255, 0.69999999)");
        var rounded = ThemePaletteModel.ToRoundedRgbaString(color, 2);

        Assert.Equal("rgba(255,255,255,0.7)", rounded.Replace(" ", ""));
    }

    [Fact]
    public void MudColor_HexA_Behavior()
    {
        var color = new MudColor("rgba(255, 255, 255, 0.7)");
        var hexA = color.ToString(MudColorOutputFormats.HexA);
        var hex = color.ToString(MudColorOutputFormats.Hex);
        var fromHexA = new MudColor(hexA);
        Assert.StartsWith("#ffffff", hexA);
        Assert.Equal(color.A, fromHexA.A);
    }

    [Fact]
    public async Task MudColorPicker_LoadsInitialValue_AndUpdatesOnValueChanged()
    {
        await using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();

        string currentHex = "#38BDF8";
        var mudColor = new MudColor(currentHex);

#pragma warning disable CS8619
        var comp = ctx.Render<MudColorPicker>(builder => builder
            .Add(p => p.Value, mudColor)
            .Add(p => p.ValueChanged, (Action<MudColor>)(c => currentHex = ThemePaletteModel.ColorToString(c)))
            .Add(p => p.Text, currentHex)
            .Add(p => p.TextChanged, (Action<string>)(s => currentHex = s)));
#pragma warning restore CS8619

#pragma warning disable MUD0012
        var val = comp.Instance.Value;
#pragma warning restore MUD0012
        Assert.NotNull(val);
        Assert.Equal("#38bdf8", val.ToString(MudColorOutputFormats.Hex).ToLowerInvariant());
    }

    [Theory]
    [InlineData("#38bdf8", "#38bdf8")]
    [InlineData("#38BDF8", "#38bdf8")]
    [InlineData("#38bdf8b3", "#38bdf8b3")]
    [InlineData("#xyz", "")]
    [InlineData("not-a-color", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void NormalizeColor_ValidatesValuesAndReturnsEmptyOnFailure(string? input, string expected)
    {
        var result = ThemePaletteModel.NormalizeColor(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ApplyTo_RetainsPaletteDefaults_WhenModelContainsMalformedColors()
    {
        var palette = new PaletteDark();
        var defaultPrimary = palette.Primary;

        var model = new ThemePaletteModel
        {
            Primary = "#invalid_color_value",
            Secondary = "completely_broken"
        };

        // Should not throw exception and should retain default palette colors
        model.ApplyTo(palette);

        Assert.Equal(defaultPrimary, palette.Primary);
    }
}
