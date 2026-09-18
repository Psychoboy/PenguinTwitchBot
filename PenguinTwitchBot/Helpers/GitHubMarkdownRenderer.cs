using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace PenguinTwitchBot.Helpers;

public static class GitHubMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseEmojiAndSmiley()
        .Build();

    private static readonly Regex AnchorRegex = new(
        @"<a\s+(?<attrs>[^>]*?)href=(?<quote>[""'])(?<url>[^""']*?)\k<quote>(?<rest>[^>]*?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AlertBlockquoteRegex = new(
        @"<blockquote>\s*<p>\s*\[!(?<type>NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*<br\s*/?>?(?<content>.*?)(?:</p>|(?=</blockquote>))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AlertBlockquoteParagraphRegex = new(
        @"<blockquote>\s*<p>\s*\[!(?<type>NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*</p>\s*(?<content>.*?)(?=</blockquote>)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex BracketIconRegex = new(
        @"\[icon:(?<name>[a-zA-Z0-9_.:/-]+)(?<args>[^\]]*)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ColonIconRegex = new(
        @":icon:(?<name>[a-zA-Z0-9_.:/-]+)(?::(?<color>[^:\s]+))?(?::(?<size>[^:\s]+))?(?::(?<style>[^:\s]+))?:",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string RenderToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var html = Markdown.ToHtml(markdown, Pipeline);

        // Transform GitHub Alerts: > [!NOTE], > [!TIP], > [!IMPORTANT], > [!WARNING], > [!CAUTION]
        html = TransformAlerts(html);

        // Transform Custom Icons: [icon:person #3299ff 1.5em] or :icon:person:#3299ff:1.5em:
        html = TransformIcons(html);

        // Ensure all external anchor tags have target="_blank" and rel="noopener noreferrer"
        html = EnsureExternalLinks(html);

        return html;
    }

    private static string TransformIcons(string html)
    {
        html = BracketIconRegex.Replace(html, match =>
        {
            var name = match.Groups["name"].Value;
            var args = match.Groups["args"].Value.Trim();
            string? color = null;
            string? size = null;
            string? style = null;

            if (!string.IsNullOrWhiteSpace(args))
            {
                var colorMatch = Regex.Match(args, @"(?:color\s*=\s*[""']?([^""'\s]+)[""']?)", RegexOptions.IgnoreCase);
                var sizeMatch = Regex.Match(args, @"(?:size\s*=\s*[""']?([^""'\s]+)[""']?)", RegexOptions.IgnoreCase);
                var styleMatch = Regex.Match(args, @"(?:style\s*=\s*[""']?([^""'\s]+)[""']?)", RegexOptions.IgnoreCase);

                if (colorMatch.Success) color = colorMatch.Groups[1].Value;
                if (sizeMatch.Success) size = sizeMatch.Groups[1].Value;
                if (styleMatch.Success) style = styleMatch.Groups[1].Value;

                var tokens = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    if (token.Contains('=')) continue;

                    if (MarkdownIconResolver.IsStyleName(token, out _))
                    {
                        style ??= token;
                    }
                    else if (token.StartsWith('#') || IsColorToken(token))
                    {
                        color ??= token;
                    }
                    else if (char.IsDigit(token[0]) || token.EndsWith("em", StringComparison.OrdinalIgnoreCase) || token.EndsWith("px", StringComparison.OrdinalIgnoreCase) || token.EndsWith("rem", StringComparison.OrdinalIgnoreCase))
                    {
                        size ??= token;
                    }
                }
            }

            var svg = MarkdownIconResolver.RenderSvg(name, color, size, style);
            return !string.IsNullOrEmpty(svg) ? svg : match.Value;
        });

        html = ColonIconRegex.Replace(html, match =>
        {
            var name = match.Groups["name"].Value;
            var color = match.Groups["color"].Success ? match.Groups["color"].Value : null;
            var size = match.Groups["size"].Success ? match.Groups["size"].Value : null;
            var style = match.Groups["style"].Success ? match.Groups["style"].Value : null;

            var svg = MarkdownIconResolver.RenderSvg(name, color, size, style);
            return !string.IsNullOrEmpty(svg) ? svg : match.Value;
        });

        return html;
    }

    private static bool IsColorToken(string token)
    {
        return !char.IsDigit(token[0]) &&
               !token.EndsWith("em", StringComparison.OrdinalIgnoreCase) &&
               !token.EndsWith("px", StringComparison.OrdinalIgnoreCase) &&
               !token.EndsWith("rem", StringComparison.OrdinalIgnoreCase) &&
               !token.EndsWith("%", StringComparison.OrdinalIgnoreCase);
    }

    private static string TransformAlerts(string html)
    {
        // First pattern: [!TYPE]<br />Content
        html = AlertBlockquoteRegex.Replace(html, match =>
        {
            var type = match.Groups["type"].Value.ToUpperInvariant();
            var content = match.Groups["content"].Value.Trim();
            var title = GetAlertTitle(type);
            var iconSvg = GetAlertIconSvg(type);

            return $@"<div class=""markdown-alert markdown-alert-{type.ToLowerInvariant()}"">
<div class=""markdown-alert-title"">{iconSvg}<span>{title}</span></div>
<p>{content}";
        });

        // Second pattern: <p>[!TYPE]</p> content
        html = AlertBlockquoteParagraphRegex.Replace(html, match =>
        {
            var type = match.Groups["type"].Value.ToUpperInvariant();
            var content = match.Groups["content"].Value.Trim();
            var title = GetAlertTitle(type);
            var iconSvg = GetAlertIconSvg(type);

            return $@"<div class=""markdown-alert markdown-alert-{type.ToLowerInvariant()}"">
<div class=""markdown-alert-title"">{iconSvg}<span>{title}</span></div>
{content}";
        });

        return html;
    }

    private static string GetAlertTitle(string type) => type switch
    {
        "NOTE" => "Note",
        "TIP" => "Tip",
        "IMPORTANT" => "Important",
        "WARNING" => "Warning",
        "CAUTION" => "Caution",
        _ => type
    };

    private static string GetAlertIconSvg(string type) => type switch
    {
        "NOTE" => @"<svg class=""octicon octicon-info"" viewBox=""0 0 16 16"" width=""16"" height=""16"" aria-hidden=""true""><path d=""M0 8a8 8 0 1 1 16 0A8 8 0 0 1 0 8Zm8-6.5a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13ZM6.5 7.75A.75.75 0 0 1 7.25 7h1a.75.75 0 0 1 .75.75v2.75h.25a.75.75 0 0 1 0 1.5h-2a.75.75 0 0 1 0-1.5h.25v-2h-.25a.75.75 0 0 1-.75-.75ZM8 6a1 1 0 1 1 0-2 1 1 0 0 1 0 2Z""></path></svg>",
        "TIP" => @"<svg class=""octicon octicon-light-bulb"" viewBox=""0 0 16 16"" width=""16"" height=""16"" aria-hidden=""true""><path d=""M8 1.5c-2.363 0-4 1.69-4 3.75 0 .984.424 1.625.984 2.304l.214.253c.223.264.47.556.673.848.284.411.537.896.621 1.49a.75.75 0 0 1-1.484.211c-.04-.282-.163-.547-.37-.847a8.456 8.456 0 0 0-.542-.68c-.084-.1-.173-.205-.268-.32C3.201 7.75 2.5 6.766 2.5 5.25 2.5 2.31 4.863 0 8 0s5.5 2.31 5.5 5.25c0 1.516-.701 2.5-1.328 3.259-.095.115-.184.22-.268.319-.18.213-.362.43-.542.681-.207.3-.33.565-.37.847a.751.751 0 0 1-1.485-.212c.084-.593.337-1.078.621-1.489.203-.292.45-.584.673-.848.075-.088.147-.173.213-.253.561-.679.985-1.32.985-2.304 0-2.06-1.637-3.75-4-3.75ZM5.75 12h4.5a.75.75 0 0 1 0 1.5h-4.5a.75.75 0 0 1 0-1.5ZM6.5 15h3a.75.75 0 0 1 0 1.5h-3a.75.75 0 0 1 0-1.5Z""></path></svg>",
        "IMPORTANT" => @"<svg class=""octicon octicon-report"" viewBox=""0 0 16 16"" width=""16"" height=""16"" aria-hidden=""true""><path d=""M0 1.75C0 .784.784 0 1.75 0h12.5C15.216 0 16 .784 16 1.75v9.5A1.75 1.75 0 0 1 14.25 13H8.06l-2.573 2.573A1.458 1.458 0 0 1 3 14.543V13H1.75A1.75 1.75 0 0 1 0 11.25Zm1.75-.25a.25.25 0 0 0-.25.25v9.5c0 .138.112.25.25.25h2a.75.75 0 0 1 .75.75v2.19l2.72-2.72a.749.749 0 0 1 .53-.22h6.5a.25.25 0 0 0 .25-.25v-9.5a.25.25 0 0 0-.25-.25Zm7 2.25v2.5a.75.75 0 0 1-1.5 0v-2.5a.75.75 0 0 1 1.5 0ZM9 9a1 1 0 1 1-2 0 1 1 0 0 1 2 0Z""></path></svg>",
        "WARNING" => @"<svg class=""octicon octicon-alert"" viewBox=""0 0 16 16"" width=""16"" height=""16"" aria-hidden=""true""><path d=""M6.457 1.047c.659-1.234 2.427-1.234 3.086 0l6.082 11.378A1.75 1.75 0 0 1 14.082 15H1.918a1.75 1.75 0 0 1-1.543-2.575Zm1.763.707a.25.25 0 0 0-.44 0L1.698 13.132a.25.25 0 0 0 .22.368h12.164a.25.25 0 0 0 .22-.368Zm.53 3.996v2.5a.75.75 0 0 1-1.5 0v-2.5a.75.75 0 0 1 1.5 0ZM9 11a1 1 0 1 1-2 0 1 1 0 0 1 2 0Z""></path></svg>",
        "CAUTION" => @"<svg class=""octicon octicon-stop"" viewBox=""0 0 16 16"" width=""16"" height=""16"" aria-hidden=""true""><path d=""M4.47.047A1.75 1.75 0 0 1 5.71 0h4.58c.464 0 .909.184 1.237.513l3.96 3.96c.329.328.513.773.513 1.237v4.58c0 .464-.184.909-.513 1.237l-3.96 3.96a1.75 1.75 0 0 1-1.237.513H5.71a1.75 1.75 0 0 1-1.237-.513l-3.96-3.96A1.75 1.75 0 0 1 0 10.29V5.71c0-.464.184-.909.513-1.237l3.96-3.96Zm.88 1.424a.25.25 0 0 0-.177.073L1.543 5.504a.25.25 0 0 0-.073.177v4.58c0 .066.026.13.073.177l3.63 3.63c.047.047.111.073.177.073h4.58c.066 0 .13-.026.177-.073l3.63-3.63c.047-.047.073-.111.073-.177V5.71c0-.066-.026-.13-.073-.177l-3.63-3.63a.25.25 0 0 0-.177-.073ZM8 4a.75.75 0 0 1 .75.75v3.5a.75.75 0 0 1-1.5 0v-3.5A.75.75 0 0 1 8 4Zm0 8a1 1 0 1 1 0-2 1 1 0 0 1 0 2Z""></path></svg>",
        _ => ""
    };

    private static string EnsureExternalLinks(string html)
    {
        return AnchorRegex.Replace(html, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var url = match.Groups["url"].Value;
            var quote = match.Groups["quote"].Value;
            var rest = match.Groups["rest"].Value;

            var fullAttrs = $"{attrs} {rest}";
            var hasTarget = fullAttrs.Contains("target=", StringComparison.OrdinalIgnoreCase);
            var hasRel = fullAttrs.Contains("rel=", StringComparison.OrdinalIgnoreCase);

            var sb = new StringBuilder("<a ");
            if (!string.IsNullOrWhiteSpace(attrs))
            {
                sb.Append(attrs.Trim()).Append(' ');
            }
            sb.Append($"href={quote}{url}{quote}");
            if (!string.IsNullOrWhiteSpace(rest))
            {
                sb.Append(' ').Append(rest.Trim());
            }
            if (!hasTarget && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                sb.Append(" target=\"_blank\"");
            }
            if (!hasRel && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                sb.Append(" rel=\"noopener noreferrer\"");
            }
            sb.Append('>');

            return sb.ToString();
        });
    }
}
