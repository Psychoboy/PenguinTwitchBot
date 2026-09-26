using System.Text.RegularExpressions;

namespace PenguinTwitchBot.Helpers
{
    /// <summary>
    /// Sanitizes untrusted viewer inputs (Twitch chat, channel point redeems, usernames, etc.)
    /// to prevent script injection (XSS/CEF RCE) in stream overlays and public views.
    /// </summary>
    public static partial class ViewerInputSanitizer
    {
        [GeneratedRegex(@"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ScriptTagRegex();

        [GeneratedRegex(@"<(?:iframe|object|embed|svg|style|meta|link|base|form|applet)\b[^<]*(?:(?!<\/(?:iframe|object|embed|svg|style|meta|link|base|form|applet)>)<[^<]*)*<\/(?:iframe|object|embed|svg|style|meta|link|base|form|applet)>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex DangerousBlockTagsRegex();

        [GeneratedRegex(@"<(?:iframe|object|embed|svg|style|meta|link|base|form|applet)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex DangerousSelfClosingTagsRegex();

        [GeneratedRegex(@"\bon\w+\s*=\s*(?:'[^']*'|""[^""]*""|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex InlineEventHandlerRegex();

        [GeneratedRegex(@"[\x00-\x1F\x7F]")]
        private static partial Regex ControlCharactersRegex();

        [GeneratedRegex(@"(?:(?:javascript|vbscript)\s*:|data\s*:\s*text\/html(?:\s*;[^\s,;]+)*\s*(?:;\s*base64\b|,))[^\s""'>]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ExecutableUriRegex();

        [GeneratedRegex(@"<[/!]?[a-zA-Z][^>]*>?", RegexOptions.CultureInvariant)]
        private static partial Regex HtmlTagRegex();

        /// <summary>
        /// Sanitizes untrusted viewer input by removing executable script blocks, dangerous HTML tags,
        /// inline event handlers, C0/DEL control characters, and executable URI schemes.
        /// Chat emoticons such as '&lt;3' or math expressions like 'x &lt; 5' are preserved.
        /// </summary>
        public static string Sanitize(string? input, bool trim = true)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // 1. Remove <script>...</script> tags and inner content
            var result = ScriptTagRegex().Replace(input, string.Empty);

            // 2. Remove dangerous block tags (iframe, object, embed, svg, style, etc.) and inner content
            result = DangerousBlockTagsRegex().Replace(result, string.Empty);
            result = DangerousSelfClosingTagsRegex().Replace(result, string.Empty);

            // 3. Remove inline event handlers (e.g. onerror=, onload=)
            result = InlineEventHandlerRegex().Replace(result, string.Empty);

            // 4. Strip C0 control characters and DEL (0x00-0x1F, 0x7F) before scheme checks
            result = ControlCharactersRegex().Replace(result, string.Empty);

            // 5. Remove executable URI schemes (javascript:, vbscript:, data:text/html)
            result = ExecutableUriRegex().Replace(result, string.Empty);

            // 6. Remove any remaining HTML tags starting with <letter, </letter, or <!
            result = HtmlTagRegex().Replace(result, string.Empty);

            return trim ? result.Trim() : result;
        }
    }
}
