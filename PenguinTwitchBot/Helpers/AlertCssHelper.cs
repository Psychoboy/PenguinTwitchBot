using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PenguinTwitchBot.Helpers
{
    public class AlertCssOptions
    {
        public string Color { get; set; } = "white";
        public int FontSize { get; set; } = 50;
        public string FontFamily { get; set; } = "Arial";
        public string FontWeight { get; set; } = "normal";
        public bool FontStyleItalic { get; set; } = false;
        public string TextAlign { get; set; } = "center";

        public int Width { get; set; } = 600;
        public bool WordWrap { get; set; } = true;

        public bool EnableStroke { get; set; } = true;
        public double StrokeWidth { get; set; } = 1.0;
        public string StrokeColor { get; set; } = "black";

        public bool EnableShadow { get; set; } = true;
        public string ShadowColor { get; set; } = "black";
        public int ShadowOffsetX { get; set; } = 1;
        public int ShadowOffsetY { get; set; } = 0;
        public int ShadowBlur { get; set; } = 5;

        public string ExtraCss { get; set; } = "";

        public AlertCssOptions Clone()
        {
            return (AlertCssOptions)MemberwiseClone();
        }
    }

    public static class AlertCssHelper
    {
        private static readonly Regex TextShadowTokenRegex = new(
            @"(?:rgba?\([^)]+\)|#[0-9a-fA-F]+|[^\s]+)",
            RegexOptions.Compiled);

        public static readonly List<string> PopularFontFamilies = new()
        {
            "Arial",
            "Impact",
            "Roboto",
            "Montserrat",
            "Poppins",
            "Bebas Neue",
            "Oswald",
            "Comic Sans MS",
            "Courier New",
            "Georgia",
            "Verdana",
            "Trebuchet MS",
            "Tahoma",
            "Times New Roman"
        };

        public static readonly List<string> FontWeights = new()
        {
            "normal",
            "bold",
            "500",
            "600",
            "700",
            "800",
            "900"
        };

        public static readonly List<string> TextAlignments = new()
        {
            "center",
            "left",
            "right"
        };

        public static readonly Dictionary<string, AlertCssOptions> Presets = new()
        {
            ["Classic White with Outline"] = new AlertCssOptions
            {
                Color = "white",
                FontSize = 50,
                FontFamily = "Arial",
                Width = 600,
                WordWrap = true,
                EnableStroke = true,
                StrokeWidth = 1.0,
                StrokeColor = "black",
                EnableShadow = true,
                ShadowColor = "black",
                ShadowOffsetX = 1,
                ShadowOffsetY = 0,
                ShadowBlur = 5
            },
            ["Impact Gamer (Yellow)"] = new AlertCssOptions
            {
                Color = "yellow",
                FontSize = 56,
                FontFamily = "Impact",
                FontWeight = "bold",
                Width = 650,
                WordWrap = true,
                EnableStroke = true,
                StrokeWidth = 2.0,
                StrokeColor = "black",
                EnableShadow = true,
                ShadowColor = "black",
                ShadowOffsetX = 2,
                ShadowOffsetY = 2,
                ShadowBlur = 6
            },
            ["Neon Cyan Glow"] = new AlertCssOptions
            {
                Color = "#00ffff",
                FontSize = 48,
                FontFamily = "Montserrat",
                FontWeight = "bold",
                Width = 600,
                WordWrap = true,
                EnableStroke = false,
                EnableShadow = true,
                ShadowColor = "#00ffff",
                ShadowOffsetX = 0,
                ShadowOffsetY = 0,
                ShadowBlur = 15
            },
            ["Fiery Crimson"] = new AlertCssOptions
            {
                Color = "#ff3333",
                FontSize = 52,
                FontFamily = "Impact",
                FontWeight = "800",
                Width = 600,
                WordWrap = true,
                EnableStroke = true,
                StrokeWidth = 1.5,
                StrokeColor = "#000000",
                EnableShadow = true,
                ShadowColor = "#ff0000",
                ShadowOffsetX = 0,
                ShadowOffsetY = 0,
                ShadowBlur = 10
            },
            ["Golden Glory"] = new AlertCssOptions
            {
                Color = "#ffd700",
                FontSize = 50,
                FontFamily = "Georgia",
                FontWeight = "bold",
                Width = 600,
                WordWrap = true,
                EnableStroke = true,
                StrokeWidth = 1.0,
                StrokeColor = "#3d2b00",
                EnableShadow = true,
                ShadowColor = "#000000",
                ShadowOffsetX = 2,
                ShadowOffsetY = 2,
                ShadowBlur = 4
            },
            ["Clean Minimalist"] = new AlertCssOptions
            {
                Color = "#ffffff",
                FontSize = 42,
                FontFamily = "Roboto",
                FontWeight = "500",
                Width = 600,
                WordWrap = true,
                EnableStroke = false,
                EnableShadow = true,
                ShadowColor = "rgba(0,0,0,0.7)",
                ShadowOffsetX = 1,
                ShadowOffsetY = 1,
                ShadowBlur = 4
            },
            ["Retro Green Terminal"] = new AlertCssOptions
            {
                Color = "#00ff66",
                FontSize = 44,
                FontFamily = "Courier New",
                FontWeight = "bold",
                Width = 600,
                WordWrap = true,
                EnableStroke = false,
                EnableShadow = true,
                ShadowColor = "#00ff66",
                ShadowOffsetX = 0,
                ShadowOffsetY = 0,
                ShadowBlur = 8
            }
        };

        public static string GenerateCss(AlertCssOptions options)
        {
            if (options == null) return string.Empty;

            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(options.Color))
                sb.Append($"color: {options.Color.Trim()};");

            if (options.FontSize > 0)
                sb.Append($"font-size: {options.FontSize}px;");

            if (!string.IsNullOrWhiteSpace(options.FontFamily))
                sb.Append($"font-family: {options.FontFamily.Trim()};");

            if (!string.IsNullOrWhiteSpace(options.FontWeight) && !options.FontWeight.Equals("normal", StringComparison.OrdinalIgnoreCase))
                sb.Append($"font-weight: {options.FontWeight.Trim()};");

            if (options.FontStyleItalic)
                sb.Append("font-style: italic;");

            if (!string.IsNullOrWhiteSpace(options.TextAlign) && !options.TextAlign.Equals("center", StringComparison.OrdinalIgnoreCase))
                sb.Append($"text-align: {options.TextAlign.Trim()};");

            if (options.Width > 0)
                sb.Append($"width: {options.Width}px;");

            if (options.WordWrap)
                sb.Append("word-wrap: break-word;");

            if (options.EnableStroke && options.StrokeWidth > 0 && !string.IsNullOrWhiteSpace(options.StrokeColor))
            {
                var strokeWidthStr = options.StrokeWidth % 1 == 0
                    ? $"{(int)options.StrokeWidth}px"
                    : $"{options.StrokeWidth.ToString("0.#", CultureInfo.InvariantCulture)}px";
                sb.Append($"-webkit-text-stroke-width: {strokeWidthStr};-webkit-text-stroke-color: {options.StrokeColor.Trim()};");
            }

            if (options.EnableShadow && !string.IsNullOrWhiteSpace(options.ShadowColor))
            {
                sb.Append($"text-shadow: {options.ShadowColor.Trim()} {FormatPx(options.ShadowOffsetX)} {FormatPx(options.ShadowOffsetY)} {FormatPx(options.ShadowBlur)};");
            }

            if (!string.IsNullOrWhiteSpace(options.ExtraCss))
            {
                var extra = options.ExtraCss.Trim();
                if (!extra.EndsWith(";")) extra += ";";
                sb.Append(extra);
            }

            return sb.ToString();
        }

        public static AlertCssOptions ParseCss(string? css)
        {
            var options = new AlertCssOptions
            {
                // Reset flags so only declared features are enabled
                EnableStroke = false,
                EnableShadow = false,
                WordWrap = false
            };

            if (string.IsNullOrWhiteSpace(css))
            {
                // Return default preset
                return Presets["Classic White with Outline"].Clone();
            }

            var rules = css.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var unhandled = new List<string>();

            foreach (var rule in rules)
            {
                var parts = rule.Split(':', 2);
                if (parts.Length != 2) continue;
                var key = parts[0].Trim().ToLowerInvariant();
                var val = parts[1].Trim();

                switch (key)
                {
                    case "color":
                        options.Color = val;
                        break;
                    case "font-size":
                        if (TryParsePx(val, out var fs)) options.FontSize = fs;
                        break;
                    case "font-family":
                        options.FontFamily = val.Trim('\'', '"');
                        break;
                    case "font-weight":
                        options.FontWeight = val;
                        break;
                    case "font-style":
                        options.FontStyleItalic = val.Equals("italic", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "text-align":
                        options.TextAlign = val.ToLowerInvariant();
                        break;
                    case "width":
                        if (TryParsePx(val, out var w)) options.Width = w;
                        break;
                    case "word-wrap":
                    case "overflow-wrap":
                        options.WordWrap = val.Contains("break-word", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "-webkit-text-stroke-width":
                        options.EnableStroke = true;
                        if (TryParseDouble(val, out var sw)) options.StrokeWidth = sw;
                        break;
                    case "-webkit-text-stroke-color":
                        options.EnableStroke = true;
                        options.StrokeColor = val;
                        break;
                    case "text-shadow":
                        ParseTextShadow(val, options);
                        break;
                    default:
                        unhandled.Add($"{key}: {val};");
                        break;
                }
            }

            options.ExtraCss = string.Join(" ", unhandled);
            return options;
        }

        private static void ParseTextShadow(string val, AlertCssOptions options)
        {
            if (string.IsNullOrWhiteSpace(val) || val.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                options.EnableShadow = false;
                return;
            }

            options.EnableShadow = true;
            var matches = TextShadowTokenRegex.Matches(val);
            var tokens = new List<string>();
            foreach (Match m in matches)
            {
                tokens.Add(m.Value.Trim());
            }

            if (tokens.Count == 0) return;

            string? colorToken = null;
            var numTokens = new List<int>();

            foreach (var token in tokens)
            {
                if (TryParsePx(token, out var px))
                {
                    numTokens.Add(px);
                }
                else
                {
                    colorToken = token;
                }
            }

            if (!string.IsNullOrEmpty(colorToken))
            {
                options.ShadowColor = colorToken;
            }

            if (numTokens.Count >= 1) options.ShadowOffsetX = numTokens[0];
            if (numTokens.Count >= 2) options.ShadowOffsetY = numTokens[1];
            if (numTokens.Count >= 3) options.ShadowBlur = numTokens[2];
        }

        private static bool TryParsePx(string val, out int px)
        {
            val = val.Replace("px", "", StringComparison.OrdinalIgnoreCase).Trim();
            return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out px);
        }

        private static bool TryParseDouble(string val, out double d)
        {
            val = val.Replace("px", "", StringComparison.OrdinalIgnoreCase).Trim();
            return double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out d);
        }

        private static string FormatPx(int val) => val == 0 ? "0" : $"{val}px";
    }
}
