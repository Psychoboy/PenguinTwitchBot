using MudBlazor;
using PenguinTwitchBot.Models.Themes;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public class ThemePaletteContrastTests
{
    [Fact]
    public void ApplyTo_ChoosesReadableTextForBrightAndDarkSwatches()
    {
        var theme = new CustomThemeModel
        {
            LightPalette = new ThemePaletteModel
            {
                Primary = "#00D8F6",
                Secondary = "#101820"
            }
        }.ToMudTheme();

        Assert.Equal(0, theme.PaletteLight.PrimaryContrastText.R);
        Assert.Equal(0, theme.PaletteLight.PrimaryContrastText.G);
        Assert.Equal(0, theme.PaletteLight.PrimaryContrastText.B);
        Assert.Equal(255, theme.PaletteLight.SecondaryContrastText.R);
        Assert.Equal(255, theme.PaletteLight.SecondaryContrastText.G);
        Assert.Equal(255, theme.PaletteLight.SecondaryContrastText.B);
    }

    [Fact]
    public void ApplyTo_UsesAnExplicitContrastOverride()
    {
        var theme = new CustomThemeModel
        {
            LightPalette = new ThemePaletteModel
            {
                Primary = "#00D8F6",
                PrimaryContrastText = "#FFFFFF"
            }
        }.ToMudTheme();

        Assert.Equal(255, theme.PaletteLight.PrimaryContrastText.R);
        Assert.Equal(255, theme.PaletteLight.PrimaryContrastText.G);
        Assert.Equal(255, theme.PaletteLight.PrimaryContrastText.B);
        Assert.True(ThemePaletteModel.GetContrastRatio("#000000", "#00D8F6") >= 4.5);
        Assert.True(ThemePaletteModel.GetContrastRatio("#FFFFFF", "#00D8F6") < 4.5);
    }
}