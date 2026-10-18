using System.Security.Cryptography;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Chat;
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
            bool allowDirectUrlOrPath = true)
        {
            // 1. Native Twitch emote fragment attached to CommandEventArgs when no explicit input is given
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

            var context = new ResolutionContext
            {
                CachedEmotes = cachedEmotes,
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

            // If primary tokens did not match a known emote/URL, check if Twitch provided a native EmoteUrl or emote fragment on the event
            // Only select an event emote fallback when no explicit primary input was supplied.
            if (resolved.Count == 0 && string.IsNullOrWhiteSpace(emoteOrImageInput))
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
            bool allowDirectUrlOrPath = true)
        {
            var urls = await ResolveMediaUrlsAsync(emoteService, eventArgs, emoteOrImageInput, fallbackUrl, allowDirectUrlOrPath);
            if (urls.Count == 0) return "";
            if (urls.Count == 1) return urls[0];
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

            // 3. Emote lookup (Twitch native, BTTV, FFZ, 7TV) via IEmoteService
            if (emoteService != null)
            {
                try
                {
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

            // 4. Relative media path with image extension (e.g. "images/droplets/drop1.png")
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
    }
}

