using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Services;
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
        IEmoteService emoteService,
        IHttpClientFactory httpClientFactory,
        ILogger<ChatController> logger,
        IDropRegistry? dropRegistry = null,
        IDropGamesSettingsService? settingsService = null) : ControllerBase
    {
        private const string BadgeCacheKey = "chat_badges_v1";
        private static readonly TimeSpan BadgeCacheDuration = TimeSpan.FromMinutes(30);
        private static readonly SemaphoreSlim UpstreamThrottle = new(10, 10);

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
            var emotes = await emoteService.GetEmotesAsync();
            return Ok(new { emotes });
        }

        /// <summary>
        /// Proxies remote images (e.g. Twitch, 7TV, BTTV emotes) to avoid CORS restrictions
        /// when decoding animated frames via ImageDecoder / Canvas.
        /// </summary>
        [HttpGet("proxy-image")]
        [ResponseCache(Duration = 3600)]
        public async Task<IActionResult> ProxyImage([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest("Invalid image URL");
            }

            if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || !IsAllowedEmoteHost(uri.Host))
            {
                return BadRequest("Invalid host");
            }

            if (!await IsAllowedGameAssetUrlAsync(url))
            {
                return StatusCode(StatusCodes.Status403Forbidden, "URL is not associated with an active drop or game asset.");
            }

            var cacheKey = $"proxy_img_{url}";
            if (!memoryCache.TryGetValue(cacheKey, out (byte[] bytes, string contentType) cached))
            {
                try
                {
                    var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
                    var client = httpClientFactory.CreateClient("Emotes");
                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                    request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");

                    await UpstreamThrottle.WaitAsync(cancellationToken);
                    HttpResponseMessage response;
                    try
                    {
                        response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    }
                    finally
                    {
                        UpstreamThrottle.Release();
                    }
                    using (response)
                    {
                    if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                    {
                        return BadRequest("Redirects are not allowed");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var safeUrl = SanitizeForLog(url);
                        logger.LogWarning("Upstream image fetch returned {StatusCode} for URL: {Url}", response.StatusCode, safeUrl);
                        return StatusCode((int)response.StatusCode);
                    }

                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest("Response is not an image");
                    }

                    const long maxBytes = 10 * 1024 * 1024; // 10 MiB
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > maxBytes)
                    {
                        return BadRequest("Image exceeds maximum allowed size");
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var ms = new MemoryStream();
                    var buffer = new byte[81920];
                    int bytesRead;
                    long totalBytesRead = 0;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        totalBytesRead += bytesRead;
                        if (totalBytesRead > maxBytes)
                        {
                            return BadRequest("Image exceeds maximum allowed size");
                        }
                        ms.Write(buffer, 0, bytesRead);
                    }

                    var bytes = ms.ToArray();
                    cached = (bytes, contentType);
                    var entryOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                        .SetSize(bytes.Length);
                    memoryCache.Set(cacheKey, cached, entryOptions);
                    }
                }
                catch (OperationCanceledException) when (HttpContext?.RequestAborted.IsCancellationRequested == true)
                {
                    return StatusCode(499);
                }
                catch (Exception ex)
                {
                    var safeUrl = SanitizeForLog(url);
                    logger.LogWarning(ex, "Failed to proxy image: {Url}", safeUrl);
                    return StatusCode(502);
                }
            }

            return File(cached.bytes, cached.contentType);
        }

        private async Task<bool> IsAllowedGameAssetUrlAsync(string url)
        {
            if (dropRegistry != null && dropRegistry.IsProxyUrlAllowed(url))
            {
                return true;
            }

            if (settingsService != null)
            {
                try
                {
                    var settings = await settingsService.GetSettingsAsync();
                    if (IsConfiguredInSettings(url, settings))
                    {
                        dropRegistry?.RegisterAllowedProxyUrl(url, TimeSpan.FromHours(1));
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to check drop games settings for allowed proxy URL: {Url}", SanitizeForLog(url));
                }
            }

            return false;
        }

        private static bool IsConfiguredInSettings(string url, DropGamesSettings settings)
        {
            if (settings.Themes == null) return false;

            foreach (var theme in settings.Themes)
            {
                if (string.Equals(theme.ParachuteCanopyImageUrl, url, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(theme.TargetPlatformImageUrl, url, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(theme.PlinkoBoardBackgroundImageUrl, url, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(theme.PlinkoPegImageUrl, url, StringComparison.OrdinalIgnoreCase) ||
                    ContainsTokenOrUrl(theme.DefaultCharacterImageUrl, url) ||
                    ContainsTokenOrUrl(theme.DefaultDropletImageUrl, url))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsTokenOrUrl(string? configuredList, string url)
        {
            if (string.IsNullOrWhiteSpace(configuredList)) return false;
            var tokens = DropGameMediaResolver.ParseMediaTokens(configuredList);
            return tokens.Any(t => string.Equals(t, url, StringComparison.OrdinalIgnoreCase));
        }

        private static readonly HashSet<string> AllowedEmoteDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "static-cdn.jtvnw.net",
            "cdn.betterttv.net",
            "cdn.7tv.app",
            "cdn.frankerfacez.com"
        };

        private static bool IsAllowedEmoteHost(string host)
        {
            return !string.IsNullOrWhiteSpace(host) && AllowedEmoteDomains.Contains(host);
        }

        private static string SanitizeForLog(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sanitized = input.Replace("\r", string.Empty).Replace("\n", string.Empty);
            return sanitized.Length > 256 ? sanitized[..256] : sanitized;
        }
    }
}

