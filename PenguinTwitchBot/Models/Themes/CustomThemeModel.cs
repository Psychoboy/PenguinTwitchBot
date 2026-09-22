using System.Globalization;
using System.Text.Json.Serialization;
using MudBlazor;
using MudBlazor.Utilities;

namespace PenguinTwitchBot.Models.Themes;

public class CustomThemeModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public bool IsEnabled { get; set; } = true;

    public ThemePaletteModel LightPalette { get; set; } = new();
    public ThemePaletteModel DarkPalette { get; set; } = new();

    public MudTheme ToMudTheme()
    {
        var theme = new MudTheme();

        LightPalette.ApplyTo(theme.PaletteLight);
        DarkPalette.ApplyTo(theme.PaletteDark);

        return theme;
    }

    public CustomThemeModel Clone(string newName)
    {
        return new CustomThemeModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = newName,
            Description = Description,
            IsDefault = false,
            IsBuiltIn = false,
            IsEnabled = true,
            LightPalette = LightPalette.Clone(),
            DarkPalette = DarkPalette.Clone()
        };
    }
}

public class ThemePaletteModel
{
    public string? Primary { get; set; }
    public string? Secondary { get; set; }
    public string? Tertiary { get; set; }
    public string? Info { get; set; }
    public string? Success { get; set; }
    public string? Warning { get; set; }
    public string? Error { get; set; }
    public string? Dark { get; set; }

    public string? Background { get; set; }
    public string? BackgroundGray { get; set; }
    public string? Surface { get; set; }

    public string? AppbarBackground { get; set; }
    public string? AppbarText { get; set; }

    public string? DrawerBackground { get; set; }
    public string? DrawerText { get; set; }
    public string? DrawerIcon { get; set; }

    public string? TextPrimary { get; set; }
    public string? TextSecondary { get; set; }
    public string? TextDisabled { get; set; }

    public string? ActionDefault { get; set; }
    public string? ActionDisabled { get; set; }
    public string? ActionDisabledBackground { get; set; }

    public string? LinesDefault { get; set; }
    public string? LinesInputs { get; set; }
    public string? TableLines { get; set; }
    public string? TableStriped { get; set; }
    public string? TableHover { get; set; }
    public string? Divider { get; set; }
    public string? DividerLight { get; set; }

    public void ApplyTo(Palette palette)
    {
        ApplyColor(Primary, c => palette.Primary = c);
        ApplyColor(Secondary, c => palette.Secondary = c);
        ApplyColor(Tertiary, c => palette.Tertiary = c);
        ApplyColor(Info, c => palette.Info = c);
        ApplyColor(Success, c => palette.Success = c);
        ApplyColor(Warning, c => palette.Warning = c);
        ApplyColor(Error, c => palette.Error = c);
        ApplyColor(Dark, c => palette.Dark = c);

        ApplyColor(Background, c => palette.Background = c);
        ApplyColor(BackgroundGray, c => palette.BackgroundGray = c);
        ApplyColor(Surface, c => palette.Surface = c);

        ApplyColor(AppbarBackground, c => palette.AppbarBackground = c);
        ApplyColor(AppbarText, c => palette.AppbarText = c);

        ApplyColor(DrawerBackground, c => palette.DrawerBackground = c);
        ApplyColor(DrawerText, c => palette.DrawerText = c);
        ApplyColor(DrawerIcon, c => palette.DrawerIcon = c);

        ApplyColor(TextPrimary, c => palette.TextPrimary = c);
        ApplyColor(TextSecondary, c => palette.TextSecondary = c);
        ApplyColor(TextDisabled, c => palette.TextDisabled = c);

        ApplyColor(ActionDefault, c => palette.ActionDefault = c);
        ApplyColor(ActionDisabled, c => palette.ActionDisabled = c);
        ApplyColor(ActionDisabledBackground, c => palette.ActionDisabledBackground = c);

        ApplyColor(LinesDefault, c => palette.LinesDefault = c);
        ApplyColor(LinesInputs, c => palette.LinesInputs = c);
        ApplyColor(TableLines, c => palette.TableLines = c);
        ApplyColor(TableStriped, c => palette.TableStriped = c);
        ApplyColor(TableHover, c => palette.TableHover = c);
        ApplyColor(Divider, c => palette.Divider = c);
        ApplyColor(DividerLight, c => palette.DividerLight = c);
    }

    private static void ApplyColor(string? colorStr, Action<MudColor> setter)
    {
        if (string.IsNullOrWhiteSpace(colorStr)) return;
        try
        {
            setter(new MudColor(colorStr));
        }
        catch
        {
            // Retain default palette color on malformed value
        }
    }

    public static string ColorToString(MudColor color)
    {
        if (color.A < 255)
        {
            return color.ToString(MudColorOutputFormats.HexA);
        }
        return color.ToString(MudColorOutputFormats.Hex);
    }

    public static string ToRoundedRgbaString(MudColor color, int decimals = 2)
    {
        if (color.A == 255)
        {
            return color.ToString(MudColorOutputFormats.Hex);
        }
        var alpha = Math.Round(color.A / 255.0, decimals);
        return $"rgba({color.R},{color.G},{color.B},{alpha.ToString("0.##", CultureInfo.InvariantCulture)})";
    }

    public static string NormalizeColor(string? colorStr)
    {
        if (string.IsNullOrWhiteSpace(colorStr)) return string.Empty;
        var trimmed = colorStr.Trim();

        try
        {
            var mudColor = new MudColor(trimmed);
            return ColorToString(mudColor);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static ThemePaletteModel FromPalette(Palette palette)
    {
        return new ThemePaletteModel
        {
            Primary = ColorToString(palette.Primary),
            Secondary = ColorToString(palette.Secondary),
            Tertiary = ColorToString(palette.Tertiary),
            Info = ColorToString(palette.Info),
            Success = ColorToString(palette.Success),
            Warning = ColorToString(palette.Warning),
            Error = ColorToString(palette.Error),
            Dark = ColorToString(palette.Dark),
            Background = ColorToString(palette.Background),
            BackgroundGray = ColorToString(palette.BackgroundGray),
            Surface = ColorToString(palette.Surface),
            AppbarBackground = ColorToString(palette.AppbarBackground),
            AppbarText = ColorToString(palette.AppbarText),
            DrawerBackground = ColorToString(palette.DrawerBackground),
            DrawerText = ColorToString(palette.DrawerText),
            DrawerIcon = ColorToString(palette.DrawerIcon),
            TextPrimary = ColorToString(palette.TextPrimary),
            TextSecondary = ColorToString(palette.TextSecondary),
            TextDisabled = ColorToString(palette.TextDisabled),
            ActionDefault = ColorToString(palette.ActionDefault),
            ActionDisabled = ColorToString(palette.ActionDisabled),
            ActionDisabledBackground = ColorToString(palette.ActionDisabledBackground),
            LinesDefault = ColorToString(palette.LinesDefault),
            LinesInputs = ColorToString(palette.LinesInputs),
            TableLines = ColorToString(palette.TableLines),
            TableStriped = ColorToString(palette.TableStriped),
            TableHover = ColorToString(palette.TableHover),
            Divider = ColorToString(palette.Divider),
            DividerLight = ColorToString(palette.DividerLight)
        };
    }

    public ThemePaletteModel Clone()
    {
        return new ThemePaletteModel
        {
            Primary = Primary,
            Secondary = Secondary,
            Tertiary = Tertiary,
            Info = Info,
            Success = Success,
            Warning = Warning,
            Error = Error,
            Dark = Dark,
            Background = Background,
            BackgroundGray = BackgroundGray,
            Surface = Surface,
            AppbarBackground = AppbarBackground,
            AppbarText = AppbarText,
            DrawerBackground = DrawerBackground,
            DrawerText = DrawerText,
            DrawerIcon = DrawerIcon,
            TextPrimary = TextPrimary,
            TextSecondary = TextSecondary,
            TextDisabled = TextDisabled,
            ActionDefault = ActionDefault,
            ActionDisabled = ActionDisabled,
            ActionDisabledBackground = ActionDisabledBackground,
            LinesDefault = LinesDefault,
            LinesInputs = LinesInputs,
            TableLines = TableLines,
            TableStriped = TableStriped,
            TableHover = TableHover,
            Divider = Divider,
            DividerLight = DividerLight
        };
    }

    public void NormalizeColors()
    {
        Primary = NormalizeColor(Primary);
        Secondary = NormalizeColor(Secondary);
        Tertiary = NormalizeColor(Tertiary);
        Info = NormalizeColor(Info);
        Success = NormalizeColor(Success);
        Warning = NormalizeColor(Warning);
        Error = NormalizeColor(Error);
        Dark = NormalizeColor(Dark);
        Background = NormalizeColor(Background);
        BackgroundGray = NormalizeColor(BackgroundGray);
        Surface = NormalizeColor(Surface);
        AppbarBackground = NormalizeColor(AppbarBackground);
        AppbarText = NormalizeColor(AppbarText);
        DrawerBackground = NormalizeColor(DrawerBackground);
        DrawerText = NormalizeColor(DrawerText);
        DrawerIcon = NormalizeColor(DrawerIcon);
        TextPrimary = NormalizeColor(TextPrimary);
        TextSecondary = NormalizeColor(TextSecondary);
        TextDisabled = NormalizeColor(TextDisabled);
        ActionDefault = NormalizeColor(ActionDefault);
        ActionDisabled = NormalizeColor(ActionDisabled);
        ActionDisabledBackground = NormalizeColor(ActionDisabledBackground);
        LinesDefault = NormalizeColor(LinesDefault);
        LinesInputs = NormalizeColor(LinesInputs);
        TableLines = NormalizeColor(TableLines);
        TableStriped = NormalizeColor(TableStriped);
        TableHover = NormalizeColor(TableHover);
        Divider = NormalizeColor(Divider);
        DividerLight = NormalizeColor(DividerLight);
    }
}

