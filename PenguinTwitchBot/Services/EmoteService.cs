using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PenguinTwitchBot.Bot.TwitchServices;

namespace PenguinTwitchBot.Services;

public interface IEmoteService
{
    /// <summary>
    /// Returns a flat map of emoteName → image URL for native Twitch emotes (global +
    /// channel-specific) plus BTTV, FFZ and 7TV emotes. Cached results remain available
    /// until a refresh replaces them.
    /// </summary>
    Task<Dictionary<string, string>> GetEmotesAsync();

    /// <summary>
    /// Fetches the latest emotes and atomically replaces the cached result.
    /// Existing cached results remain available while the refresh is in progress.
    /// </summary>
    Task<Dictionary<string, string>> RefreshEmotesAsync();

    /// <summary>
    /// Looks up an emote image URL by name (case-insensitive).
    /// If not found in cache and channel emotes may not have loaded yet, forces a refresh.
    /// </summary>
    Task<string?> ResolveEmoteUrlAsync(string emoteName);

    /// <summary>
    /// Tracks an emote seen in Twitch chat or EventSub message fragments so it is available
    /// immediately in memory without requiring an API fetch.
    /// </summary>
    void TrackEmote(string name, string url);
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
    private static readonly TimeSpan EmptyEmoteCacheDuration = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _fetchLock = new(1, 1);
    private readonly ConcurrentDictionary<string, string> _chatTrackedEmotes = new(StringComparer.OrdinalIgnoreCase);
    private string? _cachedChannelId;

    public void TrackEmote(string name, string url)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) return;
        _chatTrackedEmotes[name.Trim()] = url.Trim();
    }

    public Task<Dictionary<string, string>> GetEmotesAsync() => GetEmotesCoreAsync(forceRefresh: false);

    public Task<Dictionary<string, string>> RefreshEmotesAsync() => GetEmotesCoreAsync(forceRefresh: true);

    private async Task<Dictionary<string, string>> GetEmotesCoreAsync(bool forceRefresh)
    {
        if (!forceRefresh && memoryCache.TryGetValue(EmoteCacheKey, out Dictionary<string, string>? cachedEmotes) && cachedEmotes != null)
        {
            return MergeChatTrackedEmotes(cachedEmotes);
        }

        // Cached reads don't take this lock, so they continue using the last complete map
        // while a scheduled or manual refresh is fetching upstream emotes.
        await _fetchLock.WaitAsync();
        try
        {
            if (!forceRefresh && memoryCache.TryGetValue(EmoteCacheKey, out cachedEmotes) && cachedEmotes != null)
            {
                return MergeChatTrackedEmotes(cachedEmotes);
            }

            var channelId = await twitchService.GetBroadcasterUserId();
            var emotes = MergeChatTrackedEmotes(await FetchAllEmotesAsync(channelId));
            var isIncomplete = emotes.Count == 0 || string.IsNullOrEmpty(channelId);

            if (isIncomplete && memoryCache.TryGetValue(EmoteCacheKey, out cachedEmotes) && cachedEmotes != null)
            {
                logger.LogWarning("Emote refresh was incomplete; keeping the existing cache with {Count} emotes.", cachedEmotes.Count);
                return MergeChatTrackedEmotes(cachedEmotes);
            }

            if (!string.IsNullOrEmpty(channelId))
            {
                _cachedChannelId = channelId;
            }

            if (isIncomplete)
            {
                memoryCache.Set(EmoteCacheKey, emotes, EmptyEmoteCacheDuration);
            }
            else
            {
                memoryCache.Set(EmoteCacheKey, emotes);
            }

            logger.LogInformation("Fetched {Count} emotes (native + third-party) and cached result (ChannelId={ChannelId})", emotes.Count, channelId ?? "null");
            return emotes;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private Dictionary<string, string> MergeChatTrackedEmotes(Dictionary<string, string> source)
    {
        if (_chatTrackedEmotes.IsEmpty) return source;

        var hasMissing = false;
        foreach (var (k, _) in _chatTrackedEmotes)
        {
            if (!source.ContainsKey(k))
            {
                hasMissing = true;
                break;
            }
        }

        if (!hasMissing) return source;

        var copy = new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in _chatTrackedEmotes)
        {
            if (!copy.ContainsKey(k))
            {
                copy[k] = v;
            }
        }
        return copy;
    }

    public async Task<string?> ResolveEmoteUrlAsync(string emoteName)
    {
        if (string.IsNullOrWhiteSpace(emoteName)) return null;
        var cleanName = emoteName.Trim();

        // 1. Check chat-tracked emotes first (O(1), zero latency)
        if (_chatTrackedEmotes.TryGetValue(cleanName, out var chatUrl) && !string.IsNullOrWhiteSpace(chatUrl))
        {
            return chatUrl;
        }

        // 2. Check cached dictionary
        var emotes = await GetEmotesAsync();
        if (emotes.TryGetValue(cleanName, out var url) && !string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        // 3. If the first fetch happened before a channel ID was available, refresh on a miss
        // once the ID appears. The old cache remains readable during this fetch.
        try
        {
            var channelId = await twitchService.GetBroadcasterUserId();
            var hadMissingChannelId = string.IsNullOrEmpty(_cachedChannelId) && !string.IsNullOrEmpty(channelId);

            if (hadMissingChannelId)
            {
                emotes = await RefreshEmotesAsync();
                if (emotes.TryGetValue(cleanName, out url) && !string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to refresh emote cache while resolving '{EmoteName}'", cleanName);
        }

        return null;
    }

    private async Task<Dictionary<string, string>> FetchAllEmotesAsync(string? channelId)
    {
        var http = httpClientFactory.CreateClient("Emotes");

        var bttvGlobal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bttvChannel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ffzGlobal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ffzChannel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sevenTvGlobal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sevenTvChannel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var twitchNative = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var tasks = new List<Task>
        {
            FetchBttvGlobalAsync(http, bttvGlobal),
            FetchBttvChannelAsync(http, bttvChannel, channelId),
            FetchFfzGlobalAsync(http, ffzGlobal),
            FetchFfzChannelAsync(http, ffzChannel, channelId),
            FetchSevenTvGlobalAsync(http, sevenTvGlobal),
            FetchSevenTvChannelAsync(http, sevenTvChannel, channelId),
            FetchTwitchEmotesAsync(twitchNative)
        };

        await Task.WhenAll(tasks);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dict in new[] { bttvGlobal, bttvChannel, ffzGlobal, ffzChannel, sevenTvGlobal, sevenTvChannel, twitchNative })
        {
            foreach (var kvp in dict)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

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
