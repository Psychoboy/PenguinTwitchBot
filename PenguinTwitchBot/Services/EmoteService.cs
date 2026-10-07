using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PenguinTwitchBot.Bot.TwitchServices;

namespace PenguinTwitchBot.Services;

public interface IEmoteService
{
    /// <summary>
    /// Returns a flat map of emoteName → image URL for native Twitch emotes (global +
    /// channel-specific) plus BTTV, FFZ and 7TV emotes. Result is cached for 30 minutes.
    /// </summary>
    Task<Dictionary<string, string>> GetEmotesAsync();
}

/// <summary>
/// Fetches and caches native Twitch emotes plus third-party (BTTV/FFZ/7TV) emotes,
/// shared by the chat overlay and any UI that needs to preview messages containing emotes.
/// </summary>
public class EmoteService(
    ITwitchService twitchService,
    IMemoryCache memoryCache,
    IHttpClientFactory httpClientFactory,
    ILogger<EmoteService> logger) : IEmoteService
{
    private const string EmoteCacheKey = "chat_emotes_v1";
    private static readonly TimeSpan EmoteCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan EmptyEmoteCacheDuration = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    public async Task<Dictionary<string, string>> GetEmotesAsync()
    {
        if (memoryCache.TryGetValue(EmoteCacheKey, out Dictionary<string, string>? emotes) && emotes != null)
            return emotes;

        // Serialize cache-miss fetches so concurrent chat overlay / RaidRewards requests
        // don't each hit BTTV/FFZ/7TV/Twitch at once.
        await _fetchLock.WaitAsync();
        try
        {
            if (memoryCache.TryGetValue(EmoteCacheKey, out emotes) && emotes != null)
                return emotes;

            emotes = await FetchAllEmotesAsync();
            // Don't let a transient upstream failure pin an empty result for the full duration.
            var ttl = emotes.Count == 0 ? EmptyEmoteCacheDuration : EmoteCacheDuration;
            memoryCache.Set(EmoteCacheKey, emotes, ttl);
            logger.LogInformation("Fetched {Count} emotes (native + third-party) and cached result", emotes.Count);
            return emotes;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private async Task<Dictionary<string, string>> FetchAllEmotesAsync()
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

        // Native Twitch emotes last so they take priority over third-party emotes of the same name.
        await FetchTwitchEmotesAsync(result);

        return result;
    }

    private async Task FetchTwitchEmotesAsync(Dictionary<string, string> result)
    {
        try
        {
            var twitchEmotes = await twitchService.GetChatEmotesAsync();
            foreach (var (name, url) in twitchEmotes)
            {
                result[name] = url;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch native Twitch emotes");
        }
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
