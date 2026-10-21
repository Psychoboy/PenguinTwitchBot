using System.Text.RegularExpressions;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

/// <summary>Streamer-controlled look of the Starship overlays; the defaults reproduce the original look.</summary>
public sealed partial class StarshipAppearanceSettings
{
    public const int MaximumImagesPerList = 12;
    public const int MaximumImageUrlLength = 500;

    public static IReadOnlyList<string> FontFamilies { get; } =
        ["Barlow Condensed", "Arial Narrow", "Segoe UI", "Verdana", "Georgia", "Trebuchet MS", "Impact", "Courier New"];

    public static IReadOnlyList<string> ObjectKinds { get; } = ["wreck", "beacon", "debris", "station", "fork"];

    public string PanelColor { get; set; } = "#0a181d";
    public int PanelOpacityPercent { get; set; } = 94;
    public string TextColor { get; set; } = "#edf5ef";
    public string MutedColor { get; set; } = "#a8c1bd";
    public string AccentColor { get; set; } = "#66d0c2";
    public string HighlightColor { get; set; } = "#f4bd68";
    public string WarningColor { get; set; } = "#ef866f";
    /// <summary>When true each voyage's ship accent colour is used instead of <see cref="AccentColor"/>.</summary>
    public bool UseShipAccent { get; set; } = true;
    public string FontFamily { get; set; } = "Barlow Condensed";
    public int CornerRadiusPx { get; set; } = 7;
    public bool AnimationsEnabled { get; set; } = true;
    public bool ShowEffectChips { get; set; } = true;
    public bool ShowVoteBars { get; set; } = true;
    public bool ShowOddsPanel { get; set; } = true;
    public bool ShowPath { get; set; } = true;
    public bool ShowRouteMap { get; set; } = true;
    public bool ShowToasts { get; set; } = true;
    public bool UseBuiltInShipArt { get; set; } = true;
    public List<string> ShipImages { get; set; } = [];
    public bool UseBuiltInObjectArt { get; set; } = true;
    public Dictionary<string, List<string>> ObjectImages { get; set; } = [];

    public static StarshipAppearanceSettings NormalizeAndValidate(StarshipAppearanceSettings? appearance)
    {
        appearance ??= new StarshipAppearanceSettings();
        appearance.PanelColor = NormalizeColor(appearance.PanelColor, nameof(PanelColor));
        appearance.TextColor = NormalizeColor(appearance.TextColor, nameof(TextColor));
        appearance.MutedColor = NormalizeColor(appearance.MutedColor, nameof(MutedColor));
        appearance.AccentColor = NormalizeColor(appearance.AccentColor, nameof(AccentColor));
        appearance.HighlightColor = NormalizeColor(appearance.HighlightColor, nameof(HighlightColor));
        appearance.WarningColor = NormalizeColor(appearance.WarningColor, nameof(WarningColor));
        if (appearance.PanelOpacityPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(PanelOpacityPercent), "Panel opacity must be between 0 and 100 percent.");
        if (appearance.CornerRadiusPx is < 0 or > 24)
            throw new ArgumentOutOfRangeException(nameof(CornerRadiusPx), "Corner radius must be between 0 and 24 pixels.");

        var font = FontFamilies.FirstOrDefault(name => string.Equals(name, appearance.FontFamily?.Trim(), StringComparison.OrdinalIgnoreCase));
        appearance.FontFamily = font ?? throw new ArgumentException($"Choose one of the available fonts: {string.Join(", ", FontFamilies)}.", nameof(FontFamily));

        appearance.ShipImages = NormalizeImages(appearance.ShipImages, "ship");
        var objectImages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (kind, urls) in appearance.ObjectImages ?? [])
        {
            var knownKind = ObjectKinds.FirstOrDefault(name => string.Equals(name, kind, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"'{kind}' is not an event object type.", nameof(ObjectImages));
            objectImages[knownKind] = NormalizeImages(urls, knownKind);
        }

        foreach (var kind in ObjectKinds)
            objectImages.TryAdd(kind, []);
        appearance.ObjectImages = objectImages.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return appearance;
    }

    private static string NormalizeColor(string? value, string name)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (!ColorPattern().IsMatch(trimmed))
            throw new ArgumentException($"{name} must be a hex colour such as #66d0c2.", name);
        return trimmed.ToLowerInvariant();
    }

    private static List<string> NormalizeImages(List<string>? urls, string label)
    {
        var cleaned = (urls ?? [])
            .Select(url => url?.Trim() ?? string.Empty)
            .Where(url => url.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (cleaned.Count > MaximumImagesPerList)
            throw new ArgumentException($"Add no more than {MaximumImagesPerList} {label} images.", nameof(ObjectImages));
        foreach (var url in cleaned)
        {
            if (url.Length > MaximumImageUrlLength || !ImageUrlPattern().IsMatch(url))
                throw new ArgumentException($"'{(url.Length > 40 ? url[..40] + "..." : url)}' is not a valid image address. Use an http(s) address or a path starting with /.", nameof(ObjectImages));
        }

        return cleaned;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorPattern();

    [GeneratedRegex(@"^(https?://|/(?!/))[^\s""'<>\\]+$")]
    private static partial Regex ImageUrlPattern();
}
