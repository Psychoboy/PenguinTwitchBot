using System.Security.Cryptography;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    public static class DropGameMediaResolver
    {
        private static readonly char[] Delimiters = [',', ';', '|', '\n', '\r'];

        /// <summary>
        /// Splits a configuration string into individual image/emote tokens by comma, semicolon, pipe, newline, or whitespace.
        /// </summary>
        public static List<string> ParseMediaTokens(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return [];

            var rawTokens = input
                .Split(Delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t) && !t.StartsWith('%'))
                .ToList();

            var result = new List<string>();
            foreach (var token in rawTokens)
            {
                if (token.Contains(' ') &&
                    !token.StartsWith('/') &&
                    !token.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !token.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var subTokens = token
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(t => !string.IsNullOrEmpty(t) && !t.StartsWith('%'));
                    result.AddRange(subTokens);
                }
                else
                {
                    result.Add(token);
                }
            }

            return result;
        }

        private const int MaxMediaTokens = 10;

        /// <summary>
        /// Resolves one or more media tokens (Twitch/BTTV/FFZ/7TV emote codes or image URLs/paths) into valid CDN URLs.
        /// Supports multi-image pools (e.g. "sptvShots, sptvLights, sptv30k").
        /// </summary>
        public static async Task<List<string>> ResolveMediaUrlsAsync(
            IEmoteService? emoteService,
            CommandEventArgs? eventArgs,
            string? emoteOrImageInput,
            string? fallbackInput,
            bool allowDirectUrlOrPath = false)
        {
            // 1. Native Twitch emote fragment attached to CommandEventArgs (from Twitch chat)
            if (!string.IsNullOrWhiteSpace(eventArgs?.EmoteUrl) && string.IsNullOrWhiteSpace(emoteOrImageInput))
            {
                return [NormalizeTwitchEmoteUrl(eventArgs.EmoteUrl)];
            }

            var primaryTokens = ParseMediaTokens(emoteOrImageInput);
            if (primaryTokens.Count == 0)
            {
                primaryTokens = ParseMediaTokens(eventArgs?.Arg);
            }

            if (primaryTokens.Count > MaxMediaTokens)
            {
                primaryTokens = primaryTokens.Take(MaxMediaTokens).ToList();
            }

            var resolved = new List<string>();
            Dictionary<string, string>? cachedEmotes = null;
            if (emoteService != null)
            {
                try
                {
                    cachedEmotes = await emoteService.GetEmotesAsync();
                }
                catch
                {
                    // Ignore transient error fetching cached dictionary
                }
            }

            var context = new ResolutionContext { CachedEmotes = cachedEmotes };

            if (primaryTokens.Count > 0)
            {
                foreach (var token in primaryTokens)
                {
                    var url = await ResolveSingleTokenAsync(emoteService, token, allowDirectUrlOrPath, context);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        resolved.Add(url);
                    }
                }
            }

            // If no primary tokens resolved, resolve the fallbackInput (e.g. active theme's DefaultDropletImageUrl)
            if (resolved.Count == 0 && !string.IsNullOrWhiteSpace(fallbackInput))
            {
                var fallbackTokens = ParseMediaTokens(fallbackInput);
                if (fallbackTokens.Count > MaxMediaTokens)
                {
                    fallbackTokens = fallbackTokens.Take(MaxMediaTokens).ToList();
                }

                foreach (var token in fallbackTokens)
                {
                    // Fallback tokens come from saved theme settings and are trusted to allow local paths/URLs
                    var url = await ResolveSingleTokenAsync(emoteService, token, allowDirectUrlOrPath: true, context);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        resolved.Add(url);
                    }
                }
            }

            return resolved;
        }

        private sealed class ResolutionContext
        {
            public Dictionary<string, string>? CachedEmotes { get; set; }
            public bool HasRefreshedLookup { get; set; }
        }

        /// <summary>
        /// Resolves a character or item sprite URL. If multiple items are configured, picks one randomly.
        /// Returns empty string if no valid sprite could be resolved (allowing the overlay to use built-in seasonal art).
        /// </summary>
        public static async Task<string?> ResolveCharacterOrEmoteUrlAsync(
            IEmoteService? emoteService,
            CommandEventArgs? eventArgs,
            string? emoteOrImageInput,
            string? fallbackUrl = null,
            bool allowDirectUrlOrPath = false)
        {
            var urls = await ResolveMediaUrlsAsync(emoteService, eventArgs, emoteOrImageInput, fallbackUrl, allowDirectUrlOrPath);
            if (urls.Count == 0) return "";
            if (urls.Count == 1) return urls[0];
            return urls[RandomNumberGenerator.GetInt32(urls.Count)];
        }

        private static async Task<string?> ResolveSingleTokenAsync(
            IEmoteService? emoteService,
            string token,
            bool allowDirectUrlOrPath,
            ResolutionContext context)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            token = NormalizeTwitchEmoteUrl(token);

            // Direct URL (http:// or https://) or absolute path (/images/...)
            if (IsAllowedPathOrUrl(token))
            {
                return allowDirectUrlOrPath ? token : null;
            }

            // Emote lookup (Twitch native, BTTV, FFZ, 7TV) via IEmoteService
            if (emoteService != null)
            {
                try
                {
                    // 1. Check cached dictionary first for exact or case-insensitive match
                    var emotes = context.CachedEmotes;
                    if (emotes != null)
                    {
                        if (emotes.TryGetValue(token, out var dictUrl) && !string.IsNullOrWhiteSpace(dictUrl))
                        {
                            return NormalizeTwitchEmoteUrl(dictUrl);
                        }

                        var match = emotes.FirstOrDefault(kvp => kvp.Key.Equals(token, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrWhiteSpace(match.Value))
                        {
                            return NormalizeTwitchEmoteUrl(match.Value);
                        }
                    }

                    // 2. Fall back to refreshing lookup at most once per ResolveMediaUrlsAsync call
                    if (!context.HasRefreshedLookup)
                    {
                        context.HasRefreshedLookup = true;
                        var emoteUrl = await emoteService.ResolveEmoteUrlAsync(token);
                        try
                        {
                            context.CachedEmotes = await emoteService.GetEmotesAsync();
                        }
                        catch
                        {
                            // Ignore error refreshing dictionary
                        }

                        if (!string.IsNullOrWhiteSpace(emoteUrl))
                        {
                            return NormalizeTwitchEmoteUrl(emoteUrl);
                        }
                    }
                }
                catch
                {
                    // Fallback on lookup failure
                }
            }

            // Relative media path with image extension (e.g. "images/droplets/drop1.png")
            if (allowDirectUrlOrPath && token.Contains('.') &&
                (token.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                 token.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                 token.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                 token.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                 token.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                 token.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)))
            {
                return token.StartsWith('/') ? token : $"/{token}";
            }

            // Do NOT return unresolved plain text names like "sptvShots" because the browser
            // would attempt GET http://localhost:5000/sptvShots, resulting in 404 errors.
            return null;
        }

        public static string NormalizeTwitchEmoteUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            if (url.Contains("static-cdn.jtvnw.net/emoticons/v2/", StringComparison.OrdinalIgnoreCase))
            {
                return url.Replace("/static/", "/default/", StringComparison.OrdinalIgnoreCase);
            }
            return url;
        }

        public static bool IsAllowedPathOrUrl(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return false;
            return candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   candidate.StartsWith('/');
        }

        public static DropGameTheme ResolveTheme(DropGamesSettings settings, string? requestedThemeId, string defaultThemeId)
        {
            var targetId = !string.IsNullOrWhiteSpace(requestedThemeId) ? requestedThemeId : defaultThemeId;
            return settings.Themes.FirstOrDefault(t => t.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                ?? settings.Themes.FirstOrDefault()
                ?? DropGameTheme.CreateDefaultSeasonalThemes()[0];
        }
    }
}

