using System.Text.RegularExpressions;

namespace PenguinTwitchBot.Helpers
{
    /// <summary>
    /// Extracts YouTube video ids from the url formats song requests are allowed to use.
    /// </summary>
    public static class YouTubeUrlHelper
    {
        private static readonly string[] VideoPathPrefixes = ["shorts", "embed", "live", "v"];

        private static readonly Regex VideoIdRegex = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);

        /// <summary>
        /// Returns the video id for a raw id or any supported YouTube url, or null when the input
        /// cannot be resolved to a video id.
        /// </summary>
        public static string? ExtractVideoId(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var value = input.Trim();

            if (VideoIdRegex.IsMatch(value)) return value;

            if (!value.Contains("://", StringComparison.Ordinal))
            {
                // Allow pasting host-relative links such as youtu.be/<id> or www.youtube.com/watch?v=<id>
                value = "https://" + value;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
            if (!IsYouTubeHost(host)) return null;

            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var candidate = host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
                ? uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault()
                : query["v"] ?? ExtractFromPath(uri.AbsolutePath);

            return !string.IsNullOrWhiteSpace(candidate) && VideoIdRegex.IsMatch(candidate) ? candidate : null;
        }

        private static bool IsYouTubeHost(string host)
        {
            return host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
                || host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ExtractFromPath(string path)
        {
            var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2) return null;

            return VideoPathPrefixes.Contains(segments[0].ToLowerInvariant()) ? segments[1] : null;
        }
    }
}
