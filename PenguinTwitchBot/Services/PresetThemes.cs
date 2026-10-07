using MudBlazor;
using PenguinTwitchBot.Models.Themes;

namespace PenguinTwitchBot.Services;

public static class PresetThemes
{
    public const string DefaultThemeId = "default";
    public const string ArcticThemeId = "arctic";
    public const string MidnightPurpleThemeId = "midnight_purple";
    public const string CyberpunkThemeId = "cyberpunk";
    public const string ForestEmeraldThemeId = "forest_emerald";

    public static List<CustomThemeModel> GetPresets()
    {
        var defaultTheme = new MudTheme();

        return new List<CustomThemeModel>
        {
            new CustomThemeModel
            {
                Id = DefaultThemeId,
                Name = "Default",
                Description = "The standard default theme.",
                IsDefault = true,
                IsBuiltIn = true,
                LightPalette = ThemePaletteModel.FromPalette(defaultTheme.PaletteLight),
                DarkPalette = ThemePaletteModel.FromPalette(defaultTheme.PaletteDark)
            },
            new CustomThemeModel
            {
                Id = ArcticThemeId,
                Name = "Penguin Arctic",
                Description = "A cool arctic theme with icy blues and deep navy tones.",
                IsDefault = false,
                IsBuiltIn = true,
                LightPalette = new ThemePaletteModel
                {
                    Primary = "#0284C7",
                    Secondary = "#0EA5E9",
                    Tertiary = "#38BDF8",
                    Info = "#0284C7",
                    Success = "#10B981",
                    Warning = "#F59E0B",
                    Error = "#EF4444",
                    Background = "#F0F9FF",
                    Surface = "#FFFFFF",
                    AppbarBackground = "#0284C7",
                    AppbarText = "#FFFFFF",
                    DrawerBackground = "#FFFFFF",
                    DrawerText = "#0F172A",
                    TextPrimary = "#000000de",
                    TextSecondary = "#0000008a"
                },
                DarkPalette = new ThemePaletteModel
                {
                    Primary = "#38BDF8",
                    Secondary = "#0EA5E9",
                    Tertiary = "#7DD3FC",
                    Info = "#38BDF8",
                    Success = "#34D399",
                    Warning = "#FBBF24",
                    Error = "#F87171",
                    Background = "#0B132B",
                    Surface = "#1C2541",
                    AppbarBackground = "#1C2541",
                    AppbarText = "#F0F9FF",
                    DrawerBackground = "#0B132B",
                    DrawerText = "#F0F9FF",
                    TextPrimary = "#ffffffb3",
                    TextSecondary = "#ffffff80"
                }
            },
            new CustomThemeModel
            {
                Id = MidnightPurpleThemeId,
                Name = "Midnight Purple",
                Description = "A deep royal purple theme with vibrant violet accents.",
                IsDefault = false,
                IsBuiltIn = true,
                LightPalette = new ThemePaletteModel
                {
                    Primary = "#7B2CBF",
                    Secondary = "#9D4EDD",
                    Tertiary = "#C77DFF",
                    Info = "#5A189A",
                    Success = "#10B981",
                    Warning = "#F59E0B",
                    Error = "#E63946",
                    Background = "#FBF8FF",
                    Surface = "#FFFFFF",
                    AppbarBackground = "#7B2CBF",
                    AppbarText = "#FFFFFF",
                    DrawerBackground = "#FFFFFF",
                    DrawerText = "#1A1A2E",
                    TextPrimary = "#000000de",
                    TextSecondary = "#0000008a"
                },
                DarkPalette = new ThemePaletteModel
                {
                    Primary = "#9D4EDD",
                    Secondary = "#C77DFF",
                    Tertiary = "#E0AAFF",
                    Info = "#7B2CBF",
                    Success = "#4EBA6F",
                    Warning = "#FFB703",
                    Error = "#FF4D6D",
                    Background = "#10002B",
                    Surface = "#240046",
                    AppbarBackground = "#240046",
                    AppbarText = "#F8F9FA",
                    DrawerBackground = "#150035",
                    DrawerText = "#F8F9FA",
                    TextPrimary = "#ffffffb3",
                    TextSecondary = "#ffffff80"
                }
            },
            new CustomThemeModel
            {
                Id = CyberpunkThemeId,
                Name = "Cyberpunk",
                Description = "High contrast neon pink and electric cyan with dark aesthetic.",
                IsDefault = false,
                IsBuiltIn = true,
                LightPalette = new ThemePaletteModel
                {
                    Primary = "#E00070",
                    Secondary = "#00A896",
                    Tertiary = "#FFB703",
                    Info = "#028090",
                    Success = "#00A896",
                    Warning = "#F77F00",
                    Error = "#D62828",
                    Background = "#F8F9FA",
                    Surface = "#FFFFFF",
                    AppbarBackground = "#E00070",
                    AppbarText = "#FFFFFF",
                    DrawerBackground = "#FFFFFF",
                    DrawerText = "#111111",
                    TextPrimary = "#000000de",
                    TextSecondary = "#0000008a"
                },
                DarkPalette = new ThemePaletteModel
                {
                    Primary = "#FF007F",
                    Secondary = "#00F0FF",
                    Tertiary = "#FFE600",
                    Info = "#00F0FF",
                    Success = "#00FF9F",
                    Warning = "#FFE600",
                    Error = "#FF3366",
                    Background = "#12131C",
                    Surface = "#1D1E2C",
                    AppbarBackground = "#1D1E2C",
                    AppbarText = "#00F0FF",
                    DrawerBackground = "#0F1017",
                    DrawerText = "#FFFFFF",
                    TextPrimary = "#ffffffb3",
                    TextSecondary = "#ffffff80"
                }
            },
            new CustomThemeModel
            {
                Id = ForestEmeraldThemeId,
                Name = "Forest Emerald",
                Description = "Lush emerald green tones inspired by nature and deep woods.",
                IsDefault = false,
                IsBuiltIn = true,
                LightPalette = new ThemePaletteModel
                {
                    Primary = "#059669",
                    Secondary = "#10B981",
                    Tertiary = "#34D399",
                    Info = "#0284C7",
                    Success = "#059669",
                    Warning = "#D97706",
                    Error = "#DC2626",
                    Background = "#F0FDF4",
                    Surface = "#FFFFFF",
                    AppbarBackground = "#059669",
                    AppbarText = "#FFFFFF",
                    DrawerBackground = "#FFFFFF",
                    DrawerText = "#064E3B",
                    TextPrimary = "#000000de",
                    TextSecondary = "#0000008a"
                },
                DarkPalette = new ThemePaletteModel
                {
                    Primary = "#10B981",
                    Secondary = "#34D399",
                    Tertiary = "#6EE7B7",
                    Info = "#38BDF8",
                    Success = "#10B981",
                    Warning = "#FBBF24",
                    Error = "#F87171",
                    Background = "#062A1E",
                    Surface = "#0B3C2B",
                    AppbarBackground = "#0B3C2B",
                    AppbarText = "#ECFDF5",
                    DrawerBackground = "#041D15",
                    DrawerText = "#ECFDF5",
                    TextPrimary = "#ffffffb3",
                    TextSecondary = "#ffffff80"
                }
            }
        };
    }
}

