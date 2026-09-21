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
        if (!string.IsNullOrWhiteSpace(Primary)) palette.Primary = new MudColor(Primary);
        if (!string.IsNullOrWhiteSpace(Secondary)) palette.Secondary = new MudColor(Secondary);
        if (!string.IsNullOrWhiteSpace(Tertiary)) palette.Tertiary = new MudColor(Tertiary);
        if (!string.IsNullOrWhiteSpace(Info)) palette.Info = new MudColor(Info);
        if (!string.IsNullOrWhiteSpace(Success)) palette.Success = new MudColor(Success);
        if (!string.IsNullOrWhiteSpace(Warning)) palette.Warning = new MudColor(Warning);
        if (!string.IsNullOrWhiteSpace(Error)) palette.Error = new MudColor(Error);
        if (!string.IsNullOrWhiteSpace(Dark)) palette.Dark = new MudColor(Dark);

        if (!string.IsNullOrWhiteSpace(Background)) palette.Background = new MudColor(Background);
        if (!string.IsNullOrWhiteSpace(BackgroundGray)) palette.BackgroundGray = new MudColor(BackgroundGray);
        if (!string.IsNullOrWhiteSpace(Surface)) palette.Surface = new MudColor(Surface);

        if (!string.IsNullOrWhiteSpace(AppbarBackground)) palette.AppbarBackground = new MudColor(AppbarBackground);
        if (!string.IsNullOrWhiteSpace(AppbarText)) palette.AppbarText = new MudColor(AppbarText);

        if (!string.IsNullOrWhiteSpace(DrawerBackground)) palette.DrawerBackground = new MudColor(DrawerBackground);
        if (!string.IsNullOrWhiteSpace(DrawerText)) palette.DrawerText = new MudColor(DrawerText);
        if (!string.IsNullOrWhiteSpace(DrawerIcon)) palette.DrawerIcon = new MudColor(DrawerIcon);

        if (!string.IsNullOrWhiteSpace(TextPrimary)) palette.TextPrimary = new MudColor(TextPrimary);
        if (!string.IsNullOrWhiteSpace(TextSecondary)) palette.TextSecondary = new MudColor(TextSecondary);
        if (!string.IsNullOrWhiteSpace(TextDisabled)) palette.TextDisabled = new MudColor(TextDisabled);

        if (!string.IsNullOrWhiteSpace(ActionDefault)) palette.ActionDefault = new MudColor(ActionDefault);
        if (!string.IsNullOrWhiteSpace(ActionDisabled)) palette.ActionDisabled = new MudColor(ActionDisabled);
        if (!string.IsNullOrWhiteSpace(ActionDisabledBackground)) palette.ActionDisabledBackground = new MudColor(ActionDisabledBackground);

        if (!string.IsNullOrWhiteSpace(LinesDefault)) palette.LinesDefault = new MudColor(LinesDefault);
        if (!string.IsNullOrWhiteSpace(LinesInputs)) palette.LinesInputs = new MudColor(LinesInputs);
        if (!string.IsNullOrWhiteSpace(TableLines)) palette.TableLines = new MudColor(TableLines);
        if (!string.IsNullOrWhiteSpace(TableStriped)) palette.TableStriped = new MudColor(TableStriped);
        if (!string.IsNullOrWhiteSpace(TableHover)) palette.TableHover = new MudColor(TableHover);
        if (!string.IsNullOrWhiteSpace(Divider)) palette.Divider = new MudColor(Divider);
        if (!string.IsNullOrWhiteSpace(DividerLight)) palette.DividerLight = new MudColor(DividerLight);
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
        if (trimmed.StartsWith("#"))
        {
            return trimmed;
        }

        try
        {
            var mudColor = new MudColor(trimmed);
            return ColorToString(mudColor);
        }
        catch
        {
            return trimmed;
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

