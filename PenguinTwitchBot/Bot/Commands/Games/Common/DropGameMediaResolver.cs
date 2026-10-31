using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Chat;
using PenguinTwitchBot.Extensions;
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
        /// Best-effort parser for commands supporting optional `[power] [emote]` arguments (e.g. `!golf [power] [emote]`, `!curl [power] [emote]`).
        /// Recognizes alphanumeric emotes (such as `sptv318`) when power is omitted, and tolerates minor typos on power numbers (such as `75%`, `75p`, `75!`).
        /// </summary>
        public static async Task<(double Power, string? EmoteToken)> ParsePowerAndEmoteAsync(
            IEmoteService? emoteService,
            CommandEventArgs? eventArgs,
            double? explicitPower,
            string? explicitEmote,
            bool enableViewerPowerControl,
            int defaultPowerMin,
            int defaultPowerMax)
        {
            var tokens = new List<string>();
            var usedExplicitAsTokens = false;
            if (eventArgs?.Args != null && eventArgs.Args.Count > 0)
            {
                foreach (var arg in eventArgs.Args)
                {
                    var trimmed = arg?.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        tokens.Add(trimmed);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(eventArgs?.Arg))
            {
                var split = eventArgs.Arg.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                tokens.AddRange(split.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            else if (!string.IsNullOrWhiteSpace(explicitEmote) && !explicitPower.HasValue)
            {
                var split = explicitEmote.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                tokens.AddRange(split.Where(s => !string.IsNullOrWhiteSpace(s)));
                usedExplicitAsTokens = true;
            }

            Dictionary<string, string>? cachedEmotes = null;
            var hasLoadedCachedEmotes = false;

            var powerFound = false;
            var parsedPower = 0.0;
            string? parsedEmoteToken = null;

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                // Lazy-load cached emotes ONLY when a token starts with a digit AND contains letters (e.g. "4Head" vs "75p")
                // and wasn't already matched in eventArgs.Fragments. Pure numbers ("75") or letter-prefixed tokens ("sptv318")
                // never require fetching the emote dictionary here.
                if (!hasLoadedCachedEmotes &&
                    emoteService != null &&
                    token.Length > 1 &&
                    char.IsDigit(token[0]) &&
                    token.Any(char.IsLetter) &&
                    !IsFragmentOrPathEmote(token, eventArgs))
                {
                    hasLoadedCachedEmotes = true;
                    try
                    {
                        cachedEmotes = await emoteService.GetEmotesAsync();
                    }
                    catch
                    {
                        // Ignore transient lookup error
                    }
                }

                var isKnownEmote = IsKnownEmoteOrMediaToken(token, eventArgs, cachedEmotes);
                var hasNativeEmoteOnSingleToken = tokens.Count == 1 && !string.IsNullOrWhiteSpace(eventArgs?.EmoteUrl);

                if (!powerFound && TryParseBestEffortPower(token, isKnownEmote, hasNativeEmoteOnSingleToken, out var p))
                {
                    parsedPower = p;
                    powerFound = true;
                    continue;
                }

                if (parsedEmoteToken == null)
                {
                    parsedEmoteToken = token;
                }
            }

            var explicitTrimmed = explicitEmote?.Trim();
            var isExplicitRawArgs = !string.IsNullOrWhiteSpace(explicitTrimmed) &&
                (usedExplicitAsTokens ||
                 string.Equals(explicitTrimmed, eventArgs?.Arg?.Trim(), StringComparison.OrdinalIgnoreCase));

            var finalEmote = (!string.IsNullOrWhiteSpace(explicitTrimmed) && !isExplicitRawArgs)
                ? explicitTrimmed
                : parsedEmoteToken;

            double finalPower;
            if (explicitPower.HasValue)
            {
                finalPower = Math.Clamp(explicitPower.Value, 1.0, 100.0);
            }
            else if (enableViewerPowerControl && powerFound)
            {
                finalPower = Math.Clamp(parsedPower, 1.0, 100.0);
            }
            else
            {
                finalPower = GetRandomPowerInRange(defaultPowerMin, defaultPowerMax);
            }

            return (finalPower, finalEmote);
        }

        private static bool IsFragmentOrPathEmote(string token, CommandEventArgs? eventArgs)
        {
            if (IsAllowedPathOrUrl(token)) return true;
            if (eventArgs?.Fragments != null)
            {
                return eventArgs.Fragments.Any(f =>
                    string.Equals(f.Type, "emote", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Text?.Trim(), token, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }

        private static int GetRandomPowerInRange(int defaultMin, int defaultMax)
        {
            var min = Math.Clamp(defaultMin, 1, 100);
            var max = Math.Clamp(defaultMax, 1, 100);
            if (min > max)
            {
                (min, max) = (max, min);
            }
            return Random.Shared.Next(min, max + 1);
        }

        private static bool IsKnownEmoteOrMediaToken(
            string token,
            CommandEventArgs? eventArgs,
            Dictionary<string, string>? cachedEmotes)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            if (IsAllowedPathOrUrl(token)) return true;

            if (eventArgs?.Fragments != null)
            {
                var hasFragmentMatch = eventArgs.Fragments.Any(f =>
                    string.Equals(f.Type, "emote", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Text?.Trim(), token, StringComparison.OrdinalIgnoreCase));
                if (hasFragmentMatch)
                {
                    return true;
                }
            }

            if (cachedEmotes != null)
            {
                if (cachedEmotes.ContainsKey(token) ||
                    cachedEmotes.Keys.Any(k => k.Equals(token, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseBestEffortPower(
            string token,
            bool isKnownEmote,
            bool hasNativeEmoteOnSingleToken,
            out double power)
        {
            power = 0;
            if (string.IsNullOrWhiteSpace(token) || isKnownEmote)
            {
                return false;
            }

            // 1. Exact integer 1..100 or exact decimal 1..100
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var exactVal) &&
                exactVal is >= 1.0 and <= 100.0 &&
                char.IsDigit(token[0]))
            {
                power = Math.Round(exactVal, 1);
                return true;
            }

            // 2. Best-effort typo tolerance for tokens starting with digits (e.g. "75%", "75!", "75p", "75pts", "80s")
            // Emotes like "sptv318" start with a letter and will never enter this branch.
            if (!char.IsDigit(token[0]))
            {
                return false;
            }

            var idx = 0;
            while (idx < token.Length && (char.IsDigit(token[idx]) || token[idx] == '.'))
            {
                idx++;
            }

            if (idx == 0) return false;

            var numPart = token[..idx];
            var suffix = token[idx..].Trim();

            // If this is the only token and Twitch flagged an EmoteUrl on the message, only accept unambiguous power suffixes like "%" or "!"
            if (hasNativeEmoteOnSingleToken && suffix.Any(char.IsLetter))
            {
                return false;
            }

            // Allow short typo/unit suffixes (e.g. "%", "!", "p", "pt", "pts", "pwr", "yd", "yds", "m", or a single accidental key press)
            if (suffix.Length <= 3 &&
                double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var prefixVal) &&
                prefixVal is >= 1.0 and <= 100.0)
            {
                power = Math.Round(prefixVal, 1);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves one or more media tokens (Twitch/BTTV/FFZ/7TV emote codes or image URLs/paths) into valid CDN URLs.
        /// Supports multi-image pools (e.g. "sptvShots, sptvLights, sptv30k").
        /// </summary>
        public static async Task<List<string>> ResolveMediaUrlsAsync(
            IEmoteService? emoteService,
            CommandEventArgs? eventArgs,
            string? emoteOrImageInput,
            string? fallbackInput,
            bool allowDirectUrlOrPath = true,
            bool fallbackToEventArgsArg = true)
        {
            // 1. Native Twitch emote fragment attached to CommandEventArgs when no explicit input is given
            if (!string.IsNullOrWhiteSpace(eventArgs?.EmoteUrl) && string.IsNullOrWhiteSpace(emoteOrImageInput))
            {
                return [NormalizeTwitchEmoteUrl(eventArgs.EmoteUrl)];
            }

            // 1b. If viewer provided an explicit emote input that matches an emote fragment from chat, resolve instantly
            if (eventArgs?.Fragments != null && eventArgs.Fragments.Count > 0 && !string.IsNullOrWhiteSpace(emoteOrImageInput))
            {
                var cleanInput = emoteOrImageInput.Trim();
                var matchingFrag = eventArgs.Fragments.FirstOrDefault(f =>
                    string.Equals(f.Type, "emote", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Text?.Trim(), cleanInput, StringComparison.OrdinalIgnoreCase));
                if (matchingFrag != null)
                {
                    var fragUrl = GetFragmentEmoteUrl(matchingFrag);
                    if (!string.IsNullOrWhiteSpace(fragUrl))
                    {
                        return [fragUrl];
                    }
                }
            }

            var primaryTokens = ParseMediaTokens(emoteOrImageInput);
            if (primaryTokens.Count == 0 && fallbackToEventArgsArg)
            {
                primaryTokens = ParseMediaTokens(eventArgs?.Arg);
            }

            if (primaryTokens.Count > MaxMediaTokens)
            {
                primaryTokens = primaryTokens.Take(MaxMediaTokens).ToList();
            }

            var resolved = new List<string>();
            var context = new ResolutionContext
            {
                Fragments = eventArgs?.Fragments
            };

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

            // If primary tokens did not match a known emote/URL in the dictionary, check if Twitch provided a native EmoteUrl or emote fragment on the event
            if (resolved.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(eventArgs?.EmoteUrl))
                {
                    resolved.Add(NormalizeTwitchEmoteUrl(eventArgs.EmoteUrl));
                }
                else if (eventArgs?.Fragments != null)
                {
                    var firstEmoteFrag = eventArgs.Fragments.FirstOrDefault(f =>
                        string.Equals(f.Type, "emote", StringComparison.OrdinalIgnoreCase) &&
                        (!string.IsNullOrWhiteSpace(f.EmoteUrl) || !string.IsNullOrWhiteSpace(f.EmoteId)));
                    if (firstEmoteFrag != null)
                    {
                        var fragUrl = GetFragmentEmoteUrl(firstEmoteFrag);
                        if (!string.IsNullOrWhiteSpace(fragUrl))
                        {
                            resolved.Add(fragUrl);
                        }
                    }
                }
            }

            // If no primary tokens or native event emote resolved, resolve the fallbackInput (e.g. active theme's DefaultDropletImageUrl)
            if (resolved.Count == 0 && !string.IsNullOrWhiteSpace(fallbackInput))
            {
                var fallbackTokens = ParseMediaTokens(fallbackInput);
                if (fallbackTokens.Count > MaxMediaTokens)
                {
                    fallbackTokens = fallbackTokens.Take(MaxMediaTokens).ToList();
                }

                foreach (var token in fallbackTokens)
                {
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
            public bool HasLoadedCachedEmotes { get; set; }
            public List<ChatOverlayFragment>? Fragments { get; set; }
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
            bool allowDirectUrlOrPath = true,
            bool fallbackToEventArgsArg = true)
        {
            var urls = await ResolveMediaUrlsAsync(emoteService, eventArgs, emoteOrImageInput, fallbackUrl, allowDirectUrlOrPath, fallbackToEventArgsArg);
            if (urls.Count == 0) return "";
            if (urls.Count == 1) return urls[0];

            if (string.IsNullOrWhiteSpace(emoteOrImageInput))
            {
                var bagKey = !string.IsNullOrWhiteSpace(fallbackUrl)
                    ? $"drop_game_theme:{fallbackUrl}"
                    : $"drop_game_urls:{string.Join("|", urls)}";
                return urls.NextFromShuffleBag(bagKey) ?? urls[0];
            }

            return urls[RandomNumberGenerator.GetInt32(urls.Count)];
        }

        private static string? GetFragmentEmoteUrl(ChatOverlayFragment fragment)
        {
            if (!string.IsNullOrWhiteSpace(fragment.EmoteUrl))
            {
                return NormalizeTwitchEmoteUrl(fragment.EmoteUrl);
            }

            if (!string.IsNullOrWhiteSpace(fragment.EmoteId))
            {
                return $"https://static-cdn.jtvnw.net/emoticons/v2/{fragment.EmoteId}/default/dark/1.0";
            }

            return null;
        }

        private static async Task<string?> ResolveSingleTokenAsync(
            IEmoteService? emoteService,
            string token,
            bool allowDirectUrlOrPath,
            ResolutionContext context)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            token = NormalizeTwitchEmoteUrl(token);

            // 1. Check native Twitch emote fragments carried directly on BaseChatEventArgs.Fragments
            if (context.Fragments != null && context.Fragments.Count > 0)
            {
                var exactFrag = context.Fragments.FirstOrDefault(f =>
                    string.Equals(f.Type, "emote", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Text?.Trim(), token, StringComparison.Ordinal));
                var matchedFrag = exactFrag ?? context.Fragments.FirstOrDefault(f =>
                    string.Equals(f.Type, "emote", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Text?.Trim(), token, StringComparison.OrdinalIgnoreCase));

                if (matchedFrag != null)
                {
                    var fragUrl = GetFragmentEmoteUrl(matchedFrag);
                    if (!string.IsNullOrWhiteSpace(fragUrl))
                    {
                        return fragUrl;
                    }
                }
            }

            // 2. Direct URL (http:// or https://) or absolute path (/images/...)
            if (IsAllowedPathOrUrl(token))
            {
                return allowDirectUrlOrPath ? token : null;
            }

            // 3. Relative media path with image extension (e.g. "images/droplets/drop1.png")
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

            // Pure numeric tokens (e.g. power argument "75") are never emotes — skip emote dictionary lookup
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return null;
            }

            // 4. Emote lookup (Twitch native, BTTV, FFZ, 7TV) via IEmoteService (lazy-loaded only when needed)
            if (emoteService != null)
            {
                try
                {
                    if (!context.HasLoadedCachedEmotes)
                    {
                        context.HasLoadedCachedEmotes = true;
                        try
                        {
                            context.CachedEmotes = await emoteService.GetEmotesAsync();
                        }
                        catch
                        {
                            // Ignore transient error fetching cached dictionary
                        }
                    }

                    // Check cached dictionary first for exact or case-insensitive match
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

                    // Fall back to refreshing lookup at most once per ResolveMediaUrlsAsync call
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

        private static readonly ConcurrentDictionary<string, DropGameTheme> _preparedThemeCache = new();

        public static void ClearPreparedThemeCache()
        {
            _preparedThemeCache.Clear();
        }

        public static async Task<DropGameTheme> ResolveAndPrepareThemeAsync(
            DropGamesSettings settings,
            string? requestedThemeId,
            string defaultThemeId,
            IEmoteService? emoteService,
            IDropRegistry? dropRegistry)
        {
            var baseTheme = ResolveTheme(settings, requestedThemeId, defaultThemeId);
            var cacheKey = $"{baseTheme.Id}:{baseTheme.Name}:{baseTheme.PlinkoPegImageUrl}:{baseTheme.DefaultCharacterImageUrl}:{baseTheme.ParachuteCanopyImageUrl}:{baseTheme.TargetPlatformImageUrl}:{baseTheme.BalloonCanopyImageUrl}:{baseTheme.CurlingStoneImageUrl}:{baseTheme.GolfBallImageUrl}:{baseTheme.RacingVehicleImageUrl}:{baseTheme.RacingTrackImageUrl}:{baseTheme.RacingHazardImageUrl}:{baseTheme.RacingBoostImageUrl}";

            if (_preparedThemeCache.TryGetValue(cacheKey, out var cachedTheme))
            {
                RegisterAllThemeUrlsWithRegistry(cachedTheme, dropRegistry);
                return cachedTheme;
            }

            var resolved = baseTheme.Clone();
            var allResolved = true;

            async Task<string> ResolveAssetAsync(string? rawValue)
            {
                var (url, success) = await ResolveThemeAssetAsync(emoteService, rawValue, dropRegistry);
                if (!success) allResolved = false;
                return url;
            }

            async Task<string> ResolveMultiAssetAsync(string? rawValue)
            {
                if (string.IsNullOrWhiteSpace(rawValue)) return string.Empty;
                var tokens = ParseMediaTokens(rawValue);
                if (tokens.Count == 0) return string.Empty;

                var resolvedTokens = new List<string>();
                foreach (var token in tokens)
                {
                    var (url, success) = await ResolveThemeAssetAsync(emoteService, token, dropRegistry);
                    if (success && !string.IsNullOrWhiteSpace(url))
                    {
                        resolvedTokens.Add(url);
                    }
                    else
                    {
                        allResolved = false;
                    }
                }

                return resolvedTokens.Count > 0 ? string.Join(", ", resolvedTokens) : string.Empty;
            }

            resolved.DefaultCharacterImageUrl = await ResolveMultiAssetAsync(baseTheme.DefaultCharacterImageUrl);
            resolved.DefaultDropletImageUrl = await ResolveMultiAssetAsync(baseTheme.DefaultDropletImageUrl);
            resolved.ParachuteCanopyImageUrl = await ResolveAssetAsync(baseTheme.ParachuteCanopyImageUrl);
            resolved.TargetPlatformImageUrl = await ResolveAssetAsync(baseTheme.TargetPlatformImageUrl);
            resolved.PlinkoBoardBackgroundImageUrl = await ResolveAssetAsync(baseTheme.PlinkoBoardBackgroundImageUrl);
            resolved.PlinkoPegImageUrl = await ResolveAssetAsync(baseTheme.PlinkoPegImageUrl);
            resolved.PlinkoWinningSlotImageUrl = await ResolveAssetAsync(baseTheme.PlinkoWinningSlotImageUrl);
            resolved.PlinkoDividerImageUrl = await ResolveAssetAsync(baseTheme.PlinkoDividerImageUrl);
            resolved.BalloonCanopyImageUrl = await ResolveAssetAsync(baseTheme.BalloonCanopyImageUrl);
            resolved.BalloonCloudPlatformImageUrl = await ResolveAssetAsync(baseTheme.BalloonCloudPlatformImageUrl);
            resolved.BalloonDebrisImageUrl = await ResolveAssetAsync(baseTheme.BalloonDebrisImageUrl);
            resolved.BalloonDartImageUrl = await ResolveAssetAsync(baseTheme.BalloonDartImageUrl);
            resolved.BalloonAnvilImageUrl = await ResolveAssetAsync(baseTheme.BalloonAnvilImageUrl);
            resolved.BalloonNudgeImageUrl = await ResolveAssetAsync(baseTheme.BalloonNudgeImageUrl);
            resolved.BalloonWindGustImageUrl = await ResolveAssetAsync(baseTheme.BalloonWindGustImageUrl);
            resolved.CurlingStoneImageUrl = await ResolveAssetAsync(baseTheme.CurlingStoneImageUrl);
            resolved.CurlingHouseImageUrl = await ResolveAssetAsync(baseTheme.CurlingHouseImageUrl);
            resolved.GolfBallImageUrl = await ResolveAssetAsync(baseTheme.GolfBallImageUrl);
            resolved.GolfFlagPinImageUrl = await ResolveAssetAsync(baseTheme.GolfFlagPinImageUrl);
            resolved.GolfGreenImageUrl = await ResolveAssetAsync(baseTheme.GolfGreenImageUrl);
            resolved.RacingVehicleImageUrl = await ResolveAssetAsync(baseTheme.RacingVehicleImageUrl);
            resolved.RacingTrackImageUrl = await ResolveAssetAsync(baseTheme.RacingTrackImageUrl);
            resolved.RacingHazardImageUrl = await ResolveAssetAsync(baseTheme.RacingHazardImageUrl);
            resolved.RacingBoostImageUrl = await ResolveAssetAsync(baseTheme.RacingBoostImageUrl);

            if (allResolved)
            {
                _preparedThemeCache[cacheKey] = resolved;
            }
            return resolved;
        }

        private static void RegisterAllThemeUrlsWithRegistry(DropGameTheme theme, IDropRegistry? dropRegistry)
        {
            if (dropRegistry == null) return;
            RegisterIfValid(dropRegistry, theme.DefaultCharacterImageUrl);
            RegisterIfValid(dropRegistry, theme.DefaultDropletImageUrl);
            RegisterIfValid(dropRegistry, theme.ParachuteCanopyImageUrl);
            RegisterIfValid(dropRegistry, theme.TargetPlatformImageUrl);
            RegisterIfValid(dropRegistry, theme.PlinkoBoardBackgroundImageUrl);
            RegisterIfValid(dropRegistry, theme.PlinkoPegImageUrl);
            RegisterIfValid(dropRegistry, theme.PlinkoWinningSlotImageUrl);
            RegisterIfValid(dropRegistry, theme.PlinkoDividerImageUrl);
            RegisterIfValid(dropRegistry, theme.BalloonCanopyImageUrl);
            RegisterIfValid(dropRegistry, theme.BalloonCloudPlatformImageUrl);
            RegisterIfValid(dropRegistry, theme.BalloonDebrisImageUrl);
            RegisterIfValid(dropRegistry, theme.BalloonDartImageUrl);
            RegisterIfValid(dropRegistry, theme.BalloonAnvilImageUrl);
            RegisterIfValid(dropRegistry, theme.BalloonNudgeImageUrl);
            RegisterIfValid(dropRegistry, theme.BalloonWindGustImageUrl);
            RegisterIfValid(dropRegistry, theme.CurlingStoneImageUrl);
            RegisterIfValid(dropRegistry, theme.CurlingHouseImageUrl);
            RegisterIfValid(dropRegistry, theme.GolfBallImageUrl);
            RegisterIfValid(dropRegistry, theme.GolfFlagPinImageUrl);
            RegisterIfValid(dropRegistry, theme.GolfGreenImageUrl);
            RegisterIfValid(dropRegistry, theme.RacingVehicleImageUrl);
            RegisterIfValid(dropRegistry, theme.RacingTrackImageUrl);
            RegisterIfValid(dropRegistry, theme.RacingHazardImageUrl);
            RegisterIfValid(dropRegistry, theme.RacingBoostImageUrl);
        }

        private static void RegisterIfValid(IDropRegistry registry, string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var tokens = ParseMediaTokens(url);
            if (tokens.Count > 1)
            {
                foreach (var token in tokens)
                {
                    if (IsAllowedPathOrUrl(token))
                    {
                        registry.RegisterAllowedProxyUrl(token);
                    }
                }
            }
            else
            {
                registry.RegisterAllowedProxyUrl(url);
            }
        }

        private static async Task<(string Url, bool Success)> ResolveThemeAssetAsync(
            IEmoteService? emoteService,
            string? rawValue,
            IDropRegistry? dropRegistry)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return (string.Empty, true);
            var trimmed = rawValue.Trim();

            if (IsAllowedPathOrUrl(trimmed))
            {
                dropRegistry?.RegisterAllowedProxyUrl(trimmed);
                return (trimmed, true);
            }

            if (trimmed.Contains('.') &&
                (trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)))
            {
                var direct = trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
                dropRegistry?.RegisterAllowedProxyUrl(direct);
                return (direct, true);
            }

            var resolved = await ResolveCharacterOrEmoteUrlAsync(emoteService, null, trimmed, fallbackUrl: null, allowDirectUrlOrPath: true, fallbackToEventArgsArg: false);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return (string.Empty, false);
            }

            if (dropRegistry != null)
            {
                dropRegistry.RegisterAllowedProxyUrl(resolved);
            }

            return (resolved, true);
        }

        private static async Task<string> ResolveThemeAssetUrlAsync(
            IEmoteService? emoteService,
            string? rawValue,
            IDropRegistry? dropRegistry)
        {
            var (url, _) = await ResolveThemeAssetAsync(emoteService, rawValue, dropRegistry);
            return url;
        }

        public static DropGameEffectiveSoundDto? ResolveSound(
            DropGameSoundConfig? baseConfig,
            DropGameSoundOverride? themeOverride,
            MediaUploadService? mediaService)
        {
            if (baseConfig == null && (themeOverride == null || !themeOverride.Override))
            {
                return null;
            }

            var isOverridden = themeOverride != null && themeOverride.Override;
            var enabled = isOverridden ? themeOverride!.Enabled : (baseConfig?.Enabled ?? false);
            var file = isOverridden ? themeOverride!.AudioFile : (baseConfig?.AudioFile ?? string.Empty);
            var volume = isOverridden ? themeOverride!.Volume : (baseConfig?.Volume ?? 50);

            if (!enabled || string.IsNullOrWhiteSpace(file) || mediaService == null)
            {
                return new DropGameEffectiveSoundDto
                {
                    Enabled = false,
                    Url = null,
                    Volume = volume
                };
            }

            var resolvedUrl = mediaService.ResolveSoundUrl(file);
            if (string.IsNullOrEmpty(resolvedUrl))
            {
                return new DropGameEffectiveSoundDto
                {
                    Enabled = false,
                    Url = null,
                    Volume = volume
                };
            }

            return new DropGameEffectiveSoundDto
            {
                Enabled = true,
                Url = resolvedUrl,
                Volume = volume
            };
        }

        public static DropGameSoundBundleDto ResolveParachuteSoundBundle(
            ParachuteGameSettings settings,
            DropGameTheme theme,
            MediaUploadService? mediaService)
        {
            return new DropGameSoundBundleDto
            {
                MaxConcurrentSounds = settings.MaxConcurrentSounds,
                Start = ResolveSound(settings.StartSound, theme.ParachuteStartSound, mediaService),
                End = ResolveSound(settings.EndSound, theme.ParachuteEndSound, mediaService),
                Drop = ResolveSound(settings.DropSound, theme.ParachuteDropSound, mediaService),
                ParachuteFlap = ResolveSound(settings.ParachuteFlapSound, theme.ParachuteFlapSound, mediaService),
                Win = ResolveSound(settings.WinSound, theme.ParachuteWinSound, mediaService),
                RocketLaunch = ResolveSound(settings.RocketLaunchSound, theme.ParachuteRocketLaunchSound, mediaService),
                RocketExplode = ResolveSound(settings.RocketExplodeSound, theme.ParachuteRocketExplodeSound, mediaService),
                ParachutePop = ResolveSound(settings.ParachutePopSound, theme.ParachutePopSound, mediaService),
                CrashLand = ResolveSound(settings.CrashLandSound, theme.ParachuteCrashLandSound, mediaService),
                Cannonball = ResolveSound(settings.CannonballSound, theme.ParachuteCannonballSound, mediaService)
            };
        }

        public static DropGameSoundBundleDto ResolvePlinkoSoundBundle(
            PlinkoGameSettings settings,
            DropGameTheme theme,
            MediaUploadService? mediaService)
        {
            return new DropGameSoundBundleDto
            {
                MaxConcurrentSounds = settings.MaxConcurrentSounds,
                Start = ResolveSound(settings.StartSound, theme.PlinkoStartSound, mediaService),
                End = ResolveSound(settings.EndSound, theme.PlinkoEndSound, mediaService),
                Drop = ResolveSound(settings.DropSound, theme.PlinkoDropSound, mediaService),
                Win = ResolveSound(settings.WinSound, theme.PlinkoWinSound, mediaService),
                PegCollision = ResolveSound(settings.PegCollisionSound, theme.PlinkoPegCollisionSound, mediaService)
            };
        }

        public static DropGameSoundBundleDto ResolveBalloonSoundBundle(
            BalloonAscentGameSettings settings,
            DropGameTheme theme,
            MediaUploadService? mediaService)
        {
            return new DropGameSoundBundleDto
            {
                MaxConcurrentSounds = settings.MaxConcurrentSounds,
                Start = ResolveSound(settings.StartSound, theme.BalloonStartSound, mediaService),
                End = ResolveSound(settings.EndSound, theme.BalloonEndSound, mediaService),
                BalloonLaunch = ResolveSound(settings.LaunchSound, theme.BalloonLaunchSound, mediaService),
                BalloonPop = ResolveSound(settings.PopSound, theme.BalloonPopSound, mediaService),
                BalloonBonk = ResolveSound(settings.BonkSound, theme.BalloonBonkSound, mediaService),
                BalloonNudge = ResolveSound(settings.NudgeSound, theme.BalloonNudgeSound, mediaService),
                BalloonWin = ResolveSound(settings.WinSound, theme.BalloonWinSound, mediaService)
            };
        }

        public static DropGameSoundBundleDto ResolveCurlingSoundBundle(
            CurlingGameSettings settings,
            DropGameTheme theme,
            MediaUploadService? mediaService)
        {
            return new DropGameSoundBundleDto
            {
                MaxConcurrentSounds = settings.MaxConcurrentSounds,
                Start = ResolveSound(settings.StartSound, theme.CurlingStartSound, mediaService),
                End = ResolveSound(settings.EndSound, theme.CurlingEndSound, mediaService),
                CurlingSlide = ResolveSound(settings.SlideSound, theme.CurlingSlideSound, mediaService),
                CurlingSweep = ResolveSound(settings.SweepSound, theme.CurlingSweepSound, mediaService),
                CurlingHit = ResolveSound(settings.HitSound, theme.CurlingHitSound, mediaService),
                CurlingScore = ResolveSound(settings.ScoreSound, theme.CurlingScoreSound, mediaService),
                CurlingWin = ResolveSound(settings.WinSound, theme.CurlingWinSound, mediaService),
                CurlingFoul = ResolveSound(settings.FoulSound, theme.CurlingFoulSound, mediaService)
            };
        }

        public static DropGameSoundBundleDto ResolveGolfSoundBundle(
            GolfGameSettings settings,
            DropGameTheme theme,
            MediaUploadService? mediaService)
        {
            return new DropGameSoundBundleDto
            {
                MaxConcurrentSounds = settings.MaxConcurrentSounds,
                Start = ResolveSound(settings.StartSound, theme.GolfStartSound, mediaService),
                End = ResolveSound(settings.EndSound, theme.GolfEndSound, mediaService),
                GolfStrike = ResolveSound(settings.StrikeSound, theme.GolfStrikeSound, mediaService),
                GolfBounce = ResolveSound(settings.BounceSound, theme.GolfBounceSound, mediaService),
                GolfCup = ResolveSound(settings.CupSound, theme.GolfCupSound, mediaService),
                GolfLipOut = ResolveSound(settings.LipOutSound, theme.GolfLipOutSound, mediaService),
                GolfWin = ResolveSound(settings.WinSound, theme.GolfWinSound, mediaService)
            };
        }

        public static DropGameSoundBundleDto ResolveRacingSoundBundle(
            RacingGameSettings settings,
            DropGameTheme theme,
            MediaUploadService? mediaService)
        {
            return new DropGameSoundBundleDto
            {
                MaxConcurrentSounds = settings.MaxConcurrentSounds,
                Start = ResolveSound(settings.StartSound, theme.RacingStartSound, mediaService),
                End = ResolveSound(settings.EndSound, theme.RacingEndSound, mediaService),
                RacingStart = ResolveSound(settings.StartSound, theme.RacingStartSound, mediaService),
                RacingEnd = ResolveSound(settings.EndSound, theme.RacingEndSound, mediaService),
                RacingCountdown = ResolveSound(settings.CountdownSound, null, mediaService),
                RacingEngine = ResolveSound(settings.EngineSound, theme.RacingEngineSound, mediaService),
                RacingScreech = ResolveSound(settings.ScreechSound, theme.RacingScreechSound, mediaService),
                RacingCrash = ResolveSound(settings.CrashSound, theme.RacingCrashSound, mediaService),
                RacingBoost = ResolveSound(settings.BoostSound, theme.RacingBoostSound, mediaService),
                RacingWin = ResolveSound(settings.WinSound, theme.RacingWinSound, mediaService)
            };
        }
    }
}

