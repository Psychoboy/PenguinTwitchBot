using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    public static class DropGameMediaResolver
    {
        public static async Task<string?> ResolveCharacterOrEmoteUrlAsync(
            IEmoteService? emoteService,
            CommandEventArgs? eventArgs,
            string? emoteOrImageInput,
            string fallbackUrl,
            bool allowDirectUrlOrPath = false)
        {
            // 1. Native Twitch emote fragment attached to CommandEventArgs
            if (!string.IsNullOrWhiteSpace(eventArgs?.EmoteUrl))
            {
                return eventArgs.EmoteUrl;
            }

            var candidate = !string.IsNullOrWhiteSpace(emoteOrImageInput)
                ? emoteOrImageInput.Trim()
                : eventArgs?.Arg?.Trim();

            if (!string.IsNullOrWhiteSpace(candidate))
            {
                // Direct URL or local media path (only allowed when explicitly trusted, e.g. subaction configuration or dashboard test)
                if (allowDirectUrlOrPath && IsAllowedPathOrUrl(candidate))
                {
                    return candidate;
                }

                // First token lookup in EmoteService (Twitch + BTTV + FFZ + 7TV)
                var firstWord = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstWord) && !firstWord.StartsWith('%') && emoteService != null)
                {
                    try
                    {
                        var emotes = await emoteService.GetEmotesAsync();
                        var match = emotes.FirstOrDefault(e => e.Key.Equals(firstWord, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrWhiteSpace(match.Value))
                        {
                            return match.Value;
                        }
                    }
                    catch
                    {
                        // Fallback on lookup failure
                    }
                }
            }

            // Fallback to theme's default character/droplet image URL (empty string uses built-in seasonal sprite on overlay)
            return fallbackUrl;
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

