using System.Reflection;
using MudBlazor;

namespace PenguinTwitchBot.Helpers;

public enum MaterialIconStyle
{
    Filled,
    Outlined,
    Rounded,
    Sharp,
    TwoTone
}

public static class MarkdownIconResolver
{
    public static readonly string[] AvailableStyleNames = ["Filled", "Outlined", "Rounded", "Sharp", "TwoTone"];

    private static readonly Lazy<Dictionary<string, string>> AllCustomIcons = new(() =>
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Curated Twitch & Twitter / X vector paths
        var twitchSvg = @"<path d=""M11.571 4.714h1.715v5.143H11.57zm4.715 0H18v5.143h-1.714zM6 0L1.714 4.286v15.428h5.143V24l4.286-4.286h3.428L22.286 12V0zm14.571 11.143l-3.428 3.429h-3.429l-3 3v-3H6.857V1.714h13.714Z""/>";
        dict["twitch"] = twitchSvg;
        dict["brands:twitch"] = twitchSvg;
        dict["brands.twitch"] = twitchSvg;
        dict["custom:brands:twitch"] = twitchSvg;
        dict["custom.brands.twitch"] = twitchSvg;
        dict["icons.custom.brands.twitch"] = twitchSvg;

        var twitterSvg = @"<path d=""M23.953 4.57a10 10 0 01-2.825.775 4.958 4.958 0 002.163-2.723c-.951.555-2.005.959-3.127 1.184a4.92 4.92 0 00-8.384 4.482C7.69 8.095 4.067 6.13 1.64 3.162a4.822 4.822 0 00-.666 2.475c0 1.71.87 3.213 2.188 4.096a4.904 4.904 0 01-2.228-.616v.06a4.923 4.923 0 003.946 4.827 4.996 4.996 0 01-2.212.085 4.936 4.936 0 004.604 3.417 9.867 9.867 0 01-6.102 2.105c-.39 0-.779-.023-1.17-.067a13.995 13.995 0 007.557 2.209c9.053 0 13.998-7.496 13.998-13.985 0-.21 0-.42-.015-.63A9.936 9.936 0 0024 4.59z""/>";
        dict["twitter"] = twitterSvg;
        dict["brands:twitter"] = twitterSvg;
        dict["brands.twitter"] = twitterSvg;
        dict["custom:brands:twitter"] = twitterSvg;
        dict["custom.brands.twitter"] = twitterSvg;
        dict["icons.custom.brands.twitter"] = twitterSvg;

        // Discover all nested types in Icons.Custom (e.g. Brands, FileFormats, Uncategorized)
        foreach (var nested in typeof(Icons.Custom).GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            var subCategory = nested.Name; // "Brands", "FileFormats", "Uncategorized"
            foreach (var field in nested.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(string))
                {
                    var val = field.GetValue(null) as string;
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        var name = field.Name;
                        var lowerName = name.ToLowerInvariant();
                        var snake = ToSnakeCase(name);

                        // E.g. Icons.Custom.Brands.Steam
                        dict[$"icons.custom.{subCategory}.{name}"] = val;
                        dict[$"icons.custom.{subCategory}.{lowerName}"] = val;
                        // E.g. Custom:Brands:Steam, Custom.Brands.Steam
                        dict[$"custom:{subCategory}:{name}"] = val;
                        dict[$"custom.{subCategory}.{name}"] = val;
                        dict[$"custom:{subCategory}:{lowerName}"] = val;
                        dict[$"custom.{subCategory}.{lowerName}"] = val;
                        // E.g. Brands:Steam, Brands.Steam
                        dict[$"{subCategory}:{name}"] = val;
                        dict[$"{subCategory}.{name}"] = val;
                        dict[$"{subCategory}:{lowerName}"] = val;
                        dict[$"{subCategory}.{lowerName}"] = val;

                        // Direct field name: Steam, steam
                        dict.TryAdd(name, val);
                        dict.TryAdd(lowerName, val);

                        if (snake != lowerName)
                        {
                            dict.TryAdd(snake, val);
                            dict[$"{subCategory}:{snake}"] = val;
                            dict[$"{subCategory}.{snake}"] = val;
                            dict[$"custom:{subCategory}:{snake}"] = val;
                        }
                    }
                }
            }
        }

        // Direct aliases for crowns and royalty
        if (dict.TryGetValue("chessqueen", out var queenSvg))
        {
            dict["crown"] = queenSvg;
            dict["queen"] = queenSvg;
        }
        if (dict.TryGetValue("chessking", out var kingSvg))
        {
            dict["king"] = kingSvg;
        }

        return dict;
    });

    private static readonly Dictionary<string, string> MaterialAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Users & Profiles
        ["person"] = "Person",
        ["user"] = "Person",
        ["account"] = "Person",
        ["people"] = "People",
        ["users"] = "People",
        ["group"] = "People",

        // Chat & Communication
        ["chat"] = "Chat",
        ["message"] = "Message",
        ["forum"] = "Forum",
        ["comment"] = "Comment",
        ["comments"] = "Comment",
        ["email"] = "Email",
        ["mail"] = "Email",

        // Rewards, Stream & Gamification
        ["star"] = "Star",
        ["sub"] = "Star",
        ["subscriber"] = "Star",
        ["ticket"] = "ConfirmationNumber",
        ["tickets"] = "ConfirmationNumber",
        ["gift"] = "CardGiftcard",
        ["trophy"] = "EmojiEvents",
        ["award"] = "EmojiEvents",
        ["diamond"] = "Diamond",
        ["gem"] = "Diamond",
        ["fire"] = "Whatshot",
        ["flame"] = "Whatshot",
        ["crown"] = "ChessQueen",
        ["queen"] = "ChessQueen",
        ["king"] = "ChessKing",
        ["medal"] = "MilitaryTech",
        ["ribbon"] = "MilitaryTech",
        ["military_tech"] = "MilitaryTech",
        ["militarytech"] = "MilitaryTech",
        ["coin"] = "MonetizationOn",
        ["money"] = "MonetizationOn",
        ["game"] = "SportsEsports",
        ["gamepad"] = "SportsEsports",
        ["controller"] = "SportsEsports",

        // Common Actions & Status
        ["bell"] = "Notifications",
        ["heart"] = "Favorite",
        ["clock"] = "Schedule",
        ["timer"] = "Timer",
        ["check"] = "Check",
        ["checkmark"] = "Check",
        ["done"] = "Done",
        ["check_circle"] = "CheckCircle",
        ["checkcircle"] = "CheckCircle",
        ["success"] = "CheckCircle",
        ["warning"] = "Warning",
        ["alert"] = "Warning",
        ["info"] = "Info",
        ["error"] = "Error",
        ["bolt"] = "Bolt",
        ["lightning"] = "Bolt",
        ["shield"] = "Security",
        ["help"] = "Help",
        ["eye"] = "Visibility",
        ["lock"] = "Lock",
        ["volume"] = "VolumeUp",
        ["mute"] = "VolumeOff",
        ["music"] = "MusicNote",
        ["refresh"] = "Refresh",
        ["settings"] = "Settings",
        ["share"] = "Share",
        ["thumb_up"] = "ThumbUp",
        ["like"] = "ThumbUp",
    };

    private static readonly Dictionary<MaterialIconStyle, Lazy<Dictionary<string, string>>> IconsByStyle = new()
    {
        [MaterialIconStyle.Filled] = new(() => LoadIcons(typeof(Icons.Material.Filled))),
        [MaterialIconStyle.Outlined] = new(() => LoadIcons(typeof(Icons.Material.Outlined))),
        [MaterialIconStyle.Rounded] = new(() => LoadIcons(typeof(Icons.Material.Rounded))),
        [MaterialIconStyle.Sharp] = new(() => LoadIcons(typeof(Icons.Material.Sharp))),
        [MaterialIconStyle.TwoTone] = new(() => LoadIcons(typeof(Icons.Material.TwoTone))),
    };

    private static Dictionary<string, string> LoadIcons(Type type)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(string))
            {
                var val = field.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(val))
                {
                    dict[field.Name] = val;
                }
            }
        }
        return dict;
    }

    public static MaterialIconStyle ParseStyle(string? styleStr)
    {
        if (string.IsNullOrWhiteSpace(styleStr)) return MaterialIconStyle.Filled;
        return styleStr.Trim().ToLowerInvariant() switch
        {
            "outlined" or "outline" => MaterialIconStyle.Outlined,
            "rounded" or "round" => MaterialIconStyle.Rounded,
            "sharp" => MaterialIconStyle.Sharp,
            "twotone" or "two-tone" or "two_tone" => MaterialIconStyle.TwoTone,
            _ => MaterialIconStyle.Filled
        };
    }

    public static bool IsStyleName(string str, out MaterialIconStyle style)
    {
        var s = str.Trim().ToLowerInvariant();
        if (s is "filled" or "fill") { style = MaterialIconStyle.Filled; return true; }
        if (s is "outlined" or "outline") { style = MaterialIconStyle.Outlined; return true; }
        if (s is "rounded" or "round") { style = MaterialIconStyle.Rounded; return true; }
        if (s is "sharp") { style = MaterialIconStyle.Sharp; return true; }
        if (s is "twotone" or "two-tone" or "two_tone") { style = MaterialIconStyle.TwoTone; return true; }
        style = MaterialIconStyle.Filled;
        return false;
    }

    public static bool TryResolveIcon(string name, out string svgPath, MaterialIconStyle style = MaterialIconStyle.Filled)
    {
        svgPath = string.Empty;
        if (string.IsNullOrWhiteSpace(name)) return false;

        name = name.Trim();

        // Strip leading "Icons." if user wrote Icons.Custom.X.X or Icons.Material.X.X
        if (name.StartsWith("Icons.", StringComparison.OrdinalIgnoreCase))
        {
            name = name[6..];
        }

        if (name.StartsWith("Material.", StringComparison.OrdinalIgnoreCase))
        {
            name = name[9..];
        }

        // Check if name has style prefix or suffix, e.g. "outlined:star" or "star:outlined" or "Filled.Check"
        var split = name.Split(new[] { ':', '/', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 2)
        {
            if (IsStyleName(split[0], out var prefixStyle))
            {
                style = prefixStyle;
                name = split[1];
            }
            else if (IsStyleName(split[1], out var suffixStyle))
            {
                style = suffixStyle;
                name = split[0];
            }
        }
        else if (split.Length > 2 && split[0].Equals("Material", StringComparison.OrdinalIgnoreCase))
        {
            // e.g. Material.Outlined.Star or Material:Outlined:Star
            if (IsStyleName(split[1], out var matStyle))
            {
                style = matStyle;
                name = split[2];
            }
        }

        // 1. Check Custom Icons (Icons.Custom.Brands, Icons.Custom.FileFormats, Icons.Custom.Uncategorized)
        if (AllCustomIcons.Value.TryGetValue(name, out var customSvg))
        {
            svgPath = customSvg;
            return true;
        }

        var normalizedCustom = name.Replace('.', ':');
        if (AllCustomIcons.Value.TryGetValue(normalizedCustom, out customSvg))
        {
            svgPath = customSvg;
            return true;
        }

        if (!normalizedCustom.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
        {
            if (AllCustomIcons.Value.TryGetValue($"custom:{normalizedCustom}", out customSvg))
            {
                svgPath = customSvg;
                return true;
            }
        }

        // 2. Resolve canonical name through MaterialAliases or direct name
        var lookupName = MaterialAliases.TryGetValue(name, out var alias) ? alias : name;

        // Check if canonical alias points to a custom icon (e.g. ChessQueen, ChessKing)
        if (AllCustomIcons.Value.TryGetValue(lookupName, out var lookupCustomSvg))
        {
            svgPath = lookupCustomSvg;
            return true;
        }

        // 3. Look in requested Material style
        if (IconsByStyle.TryGetValue(style, out var lazyDict) && lazyDict.Value.TryGetValue(lookupName, out var styledSvg))
        {
            svgPath = styledSvg;
            return true;
        }

        // 4. Fallback to Filled if requested style didn't have it
        if (style != MaterialIconStyle.Filled &&
            IconsByStyle.TryGetValue(MaterialIconStyle.Filled, out var filledDict) &&
            filledDict.Value.TryGetValue(lookupName, out var filledSvg))
        {
            svgPath = filledSvg;
            return true;
        }

        // 5. Final fallback to custom icons dictionary by simple name
        if (AllCustomIcons.Value.TryGetValue(name, out var fallbackCustom))
        {
            svgPath = fallbackCustom;
            return true;
        }

        return false;
    }

    public static bool TryResolveIcon(string name, out string svgPath) => TryResolveIcon(name, out svgPath, MaterialIconStyle.Filled);

    public static string RenderSvg(string iconName, string? color = null, string? size = null, MaterialIconStyle style = MaterialIconStyle.Filled)
    {
        if (!TryResolveIcon(iconName, out var svgContent, style))
        {
            return string.Empty;
        }

        var resolvedColor = ResolveColor(color);
        var resolvedSize = !string.IsNullOrWhiteSpace(size) ? size.Trim() : "1.25em";

        if (double.TryParse(resolvedSize, out _))
        {
            resolvedSize += "px";
        }

        return $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 24 24"" width=""{resolvedSize}"" height=""{resolvedSize}"" class=""markdown-inline-icon"" fill=""{resolvedColor}"" style=""fill: {resolvedColor}; vertical-align: -0.2em; display: inline-block;"">{svgContent}</svg>";
    }

    public static string RenderSvg(string iconName, string? color, string? size, string? styleStr)
    {
        return RenderSvg(iconName, color, size, ParseStyle(styleStr));
    }

    private static string ResolveColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return "currentColor";
        }

        color = color.Trim();

        return color.ToLowerInvariant() switch
        {
            "primary" => "var(--mud-palette-primary)",
            "secondary" => "var(--mud-palette-secondary)",
            "success" => "var(--mud-palette-success)",
            "warning" => "var(--mud-palette-warning)",
            "error" => "var(--mud-palette-error)",
            "info" => "var(--mud-palette-info)",
            _ => color
        };
    }

    private static string ToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < str.Length; i++)
        {
            var c = str[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && char.IsLower(str[i - 1]))
                {
                    sb.Append('_');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public sealed record MarkdownIconModel(string Name, string DisplayName, string Category, bool IsMaterial = false)
    {
        public string GetSvgPath(MaterialIconStyle style = MaterialIconStyle.Filled)
        {
            return TryResolveIcon(Name, out var svg, style) ? svg : string.Empty;
        }
    }

    private static readonly Lazy<List<MarkdownIconModel>> AllIconsCatalog = new(() =>
    {
        var list = new List<MarkdownIconModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIcon(string name, string displayName, string category, bool isMaterial)
        {
            if (seen.Add(name))
            {
                list.Add(new MarkdownIconModel(name, displayName, category, isMaterial));
            }
        }

        // 1. Curated Stream & Rewards
        AddIcon("Star", "Star / Sub", "Stream & Rewards", isMaterial: true);
        AddIcon("ConfirmationNumber", "Tickets", "Stream & Rewards", isMaterial: true);
        AddIcon("CardGiftcard", "Gift", "Stream & Rewards", isMaterial: true);
        AddIcon("EmojiEvents", "Trophy", "Stream & Rewards", isMaterial: true);
        AddIcon("Diamond", "Diamond", "Stream & Rewards", isMaterial: true);
        AddIcon("Whatshot", "Fire", "Stream & Rewards", isMaterial: true);
        AddIcon("ChessQueen", "Crown", "Stream & Rewards", isMaterial: false);
        AddIcon("MilitaryTech", "Medal / Ribbon", "Stream & Rewards", isMaterial: true);
        AddIcon("MonetizationOn", "Coin / Money", "Stream & Rewards", isMaterial: true);
        AddIcon("SportsEsports", "Game / Controller", "Stream & Rewards", isMaterial: true);

        // 2. Custom Brands (Icons.Custom.Brands + Twitch)
        AddIcon("twitch", "Twitch", "Brands", isMaterial: false);
        foreach (var field in typeof(Icons.Custom.Brands).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(string))
            {
                AddIcon(field.Name, field.Name, "Brands", isMaterial: false);
            }
        }

        // 3. Custom File Formats (Icons.Custom.FileFormats)
        foreach (var field in typeof(Icons.Custom.FileFormats).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(string))
            {
                AddIcon(field.Name, field.Name, "File Formats", isMaterial: false);
            }
        }

        // 4. Custom Uncategorized (Icons.Custom.Uncategorized)
        foreach (var field in typeof(Icons.Custom.Uncategorized).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(string))
            {
                AddIcon(field.Name, field.Name, "Custom & Fun", isMaterial: false);
            }
        }

        // 5. Curated Users, Communication, Status
        AddIcon("Person", "Person / User", "Users & Profiles", isMaterial: true);
        AddIcon("People", "People / Group", "Users & Profiles", isMaterial: true);

        AddIcon("Chat", "Chat", "Communication", isMaterial: true);
        AddIcon("Message", "Message", "Communication", isMaterial: true);
        AddIcon("Forum", "Forum", "Communication", isMaterial: true);

        AddIcon("Notifications", "Bell / Alert", "Status & Alerts", isMaterial: true);
        AddIcon("Favorite", "Heart", "Status & Alerts", isMaterial: true);
        AddIcon("Check", "Check", "Status & Alerts", isMaterial: true);
        AddIcon("CheckCircle", "Check Circle", "Status & Alerts", isMaterial: true);
        AddIcon("Warning", "Warning", "Status & Alerts", isMaterial: true);
        AddIcon("Info", "Info", "Status & Alerts", isMaterial: true);
        AddIcon("Error", "Error", "Status & Alerts", isMaterial: true);
        AddIcon("Schedule", "Clock", "Status & Alerts", isMaterial: true);
        AddIcon("Timer", "Timer", "Status & Alerts", isMaterial: true);
        AddIcon("Bolt", "Lightning / Bolt", "Status & Alerts", isMaterial: true);
        AddIcon("Security", "Shield", "Status & Alerts", isMaterial: true);
        AddIcon("Help", "Help", "Status & Alerts", isMaterial: true);
        AddIcon("Visibility", "Eye / Visibility", "Status & Alerts", isMaterial: true);

        // 6. All Material Icons across all 5 styles
        var allMaterialNames = IconsByStyle.Values
            .SelectMany(dict => dict.Value.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k);

        foreach (var name in allMaterialNames)
        {
            AddIcon(name, name, "All Material Icons", isMaterial: true);
        }

        return list;
    });

    public static IReadOnlyList<MarkdownIconModel> GetAllIcons() => AllIconsCatalog.Value;

    public static IEnumerable<MarkdownIconModel> SearchIcons(string? query, string? category = null, int maxResults = 100)
    {
        var icons = AllIconsCatalog.Value.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (category.Equals("All Material Icons", StringComparison.OrdinalIgnoreCase))
            {
                icons = icons.Where(i => i.IsMaterial);
            }
            else
            {
                icons = icons.Where(i => i.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();

            // Extract last identifier if user typed or pasted C# path like Icons.Material.Filled.Check
            if (q.Contains('.'))
            {
                var dotParts = q.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (dotParts.Length > 0)
                {
                    q = dotParts[^1];
                }
            }
            if (q.Contains(':'))
            {
                var colonParts = q.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (colonParts.Length > 0)
                {
                    q = colonParts[^1];
                }
            }
            q = q.Trim();

            icons = icons.Where(i =>
                i.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return icons.Take(maxResults);
    }
}
