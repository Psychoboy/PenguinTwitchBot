using System.Text.RegularExpressions;

namespace PenguinTwitchBot.Helpers
{
    /// <summary>
    /// Extracts YouTube video ids from the url formats song requests are allowed to use.
    /// </summary>
    public static partial class YouTubeUrlHelper
    {
        [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
        private static partial Regex VideoIdRegex();

        /// <summary>
        /// Returns the video id for a raw id or any supported YouTube url, or null when the input
        /// cannot be resolved to a video id.
        /// </summary>
        public static string? ExtractVideoId(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var value = input.Trim();

            if (VideoIdRegex().IsMatch(value)) return value;

            if (!value.Contains("://", StringComparison.Ordinal))
            {
                // Allow pasting host-relative links such as youtu.be/<id> or www.youtube.com/watch?v=<id>
                value = "https://" + value;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;

            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var candidate = host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
                ? uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault()
                : query["v"] ?? ExtractFromPath(uri.AbsolutePath);

            return !string.IsNullOrWhiteSpace(candidate) && VideoIdRegex().IsMatch(candidate) ? candidate : null;
        }

        private static string? ExtractFromPath(string path)
        {
            var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2) return null;

            return segments[0].ToLowerInvariant() switch
            {
                "shorts" or "embed" or "live" or "v" => segments[1],
                _ => null
            };
        }
    }
}
