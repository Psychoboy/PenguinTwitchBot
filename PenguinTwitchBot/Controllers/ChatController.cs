using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PenguinTwitchBot.Bot.TwitchServices;
using System.Text.Json;

namespace PenguinTwitchBot.Controllers
{
    /// <summary>
    /// Provides data for the chat overlay widget.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController(
        ITwitchService twitchService,
        IMemoryCache memoryCache,
        IHttpClientFactory httpClientFactory,
        ILogger<ChatController> logger) : ControllerBase
    {
        private const string BadgeCacheKey = "chat_badges_v1";
        private const string EmoteCacheKey  = "chat_emotes_v1";
        private static readonly TimeSpan BadgeCacheDuration = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan EmoteCacheDuration = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Returns a flat map of "setId/versionId" → image URL (1x) for all badges.
        /// Combines global Twitch badges with channel-specific badges.
        /// Result is cached for 30 minutes.
        /// </summary>
        [HttpGet("badges")]
        public async Task<IActionResult> GetBadges()
        {
            if (!memoryCache.TryGetValue(BadgeCacheKey, out Dictionary<string, string>? badges))
            {
                badges = await twitchService.GetChatBadgesAsync();
                memoryCache.Set(BadgeCacheKey, badges, BadgeCacheDuration);
                logger.LogInformation("Fetched {Count} chat badges and cached result", badges.Count);
            }

            return Ok(new { badges });
        }

        /// <summary>
        /// Returns a flat map of emoteName → image URL for BTTV and 7TV emotes
        /// (global + channel-specific). Result is cached for 30 minutes.
        /// </summary>
        [HttpGet("emotes")]
        public async Task<IActionResult> GetEmotes()
        {
            if (!memoryCache.TryGetValue(EmoteCacheKey, out Dictionary<string, string>? emotes))
            {
                emotes = await FetchThirdPartyEmotesAsync();
                memoryCache.Set(EmoteCacheKey, emotes, EmoteCacheDuration);
                logger.LogInformation("Fetched {Count} third-party emotes and cached result", emotes.Count);
            }

            return Ok(new { emotes });
        }

        private async Task<Dictionary<string, string>> FetchThirdPartyEmotesAsync()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var http = httpClientFactory.CreateClient("Emotes");
            var channelId = await twitchService.GetBroadcasterUserId();

            await FetchBttvGlobalAsync(http, result);
            await FetchBttvChannelAsync(http, result, channelId);
            await FetchFfzGlobalAsync(http, result);
            await FetchFfzChannelAsync(http, result, channelId);
            await FetchSevenTvGlobalAsync(http, result);
            await FetchSevenTvChannelAsync(http, result, channelId);

            return result;
        }

        private async Task FetchBttvGlobalAsync(HttpClient http, Dictionary<string, string> result)
        {
            try
            {
                var resp = await http.GetAsync("https://api.betterttv.net/3/cached/emotes/global");
                if (!resp.IsSuccessStatusCode) return;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                foreach (var emote in doc.RootElement.EnumerateArray())
                {
                    if (emote.TryGetProperty("code", out var code) &&
                        emote.TryGetProperty("id", out var id))
                    {
                        result[code.GetString()!] = $"https://cdn.betterttv.net/emote/{id.GetString()}/1x";
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch BTTV global emotes");
            }
        }

        private async Task FetchBttvChannelAsync(HttpClient http, Dictionary<string, string> result, string? channelId)
        {
            if (string.IsNullOrEmpty(channelId)) return;
            try
            {
                var resp = await http.GetAsync($"https://api.betterttv.net/3/cached/users/twitch/{channelId}");
                if (!resp.IsSuccessStatusCode) return;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                foreach (var prop in new[] { "channelEmotes", "sharedEmotes" })
                {
                    if (doc.RootElement.TryGetProperty(prop, out var arr))
                    {
                        foreach (var emote in arr.EnumerateArray())
                        {
                            if (emote.TryGetProperty("code", out var code) &&
                                emote.TryGetProperty("id", out var id))
                            {
                                result[code.GetString()!] = $"https://cdn.betterttv.net/emote/{id.GetString()}/1x";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch BTTV channel emotes for {ChannelId}", channelId);
            }
        }

        private async Task FetchSevenTvGlobalAsync(HttpClient http, Dictionary<string, string> result)
        {
            try
            {
                var resp = await http.GetAsync("https://7tv.io/v3/emote-sets/global");
                if (!resp.IsSuccessStatusCode) return;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("emotes", out var emotes))
                    ParseSevenTvEmotes(emotes, result);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch 7TV global emotes");
            }
        }

        private async Task FetchFfzGlobalAsync(HttpClient http, Dictionary<string, string> result)
        {
            try
            {
                var resp = await http.GetAsync("https://api.frankerfacez.com/v1/set/global");
                if (!resp.IsSuccessStatusCode) return;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (!doc.RootElement.TryGetProperty("sets", out var sets)) return;
                foreach (var set in sets.EnumerateObject())
                {
                    if (set.Value.TryGetProperty("emoticons", out var emoticons))
                        ParseFfzEmoticons(emoticons, result);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch FFZ global emotes");
            }
        }

        private async Task FetchFfzChannelAsync(HttpClient http, Dictionary<string, string> result, string? channelId)
        {
            if (string.IsNullOrEmpty(channelId)) return;
            try
            {
                var resp = await http.GetAsync($"https://api.frankerfacez.com/v1/room/id/{channelId}");
                if (!resp.IsSuccessStatusCode) return;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (!doc.RootElement.TryGetProperty("sets", out var sets)) return;
                foreach (var set in sets.EnumerateObject())
                {
                    if (set.Value.TryGetProperty("emoticons", out var emoticons))
                        ParseFfzEmoticons(emoticons, result);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch FFZ channel emotes for {ChannelId}", channelId);
            }
        }

        private static void ParseFfzEmoticons(JsonElement emoticons, Dictionary<string, string> result)
        {
            foreach (var emote in emoticons.EnumerateArray())
            {
                if (!emote.TryGetProperty("name", out var name)) continue;
                if (!emote.TryGetProperty("urls", out var urls)) continue;
                // urls keys are "1", "2", "4" — prefer 1x
                if (!urls.TryGetProperty("1", out var url1)) continue;
                var rawUrl = url1.GetString();
                if (string.IsNullOrEmpty(rawUrl)) continue;
                result[name.GetString()!] = rawUrl.StartsWith("//") ? $"https:{rawUrl}" : rawUrl;
            }
        }

        private async Task FetchSevenTvChannelAsync(HttpClient http, Dictionary<string, string> result, string? channelId)
        {
            if (string.IsNullOrEmpty(channelId)) return;
            try
            {
                var resp = await http.GetAsync($"https://7tv.io/v3/users/twitch/{channelId}");
                if (!resp.IsSuccessStatusCode) return;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("emote_set", out var set) &&
                    set.TryGetProperty("emotes", out var emotes))
                {
                    ParseSevenTvEmotes(emotes, result);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch 7TV channel emotes for {ChannelId}", channelId);
            }
        }

        private static void ParseSevenTvEmotes(JsonElement emotes, Dictionary<string, string> result)
        {
            foreach (var emote in emotes.EnumerateArray())
            {
                if (!emote.TryGetProperty("name", out var name)) continue;
                if (!emote.TryGetProperty("data", out var data)) continue;
                if (!data.TryGetProperty("host", out var host)) continue;
                if (!host.TryGetProperty("url", out var url)) continue;
                var hostUrl = url.GetString();
                if (string.IsNullOrEmpty(hostUrl)) continue;
                // host.url is protocol-relative (//cdn.7tv.app/emote/...)
                result[name.GetString()!] = $"https:{hostUrl}/1x.webp";
            }
        }
    }
}
