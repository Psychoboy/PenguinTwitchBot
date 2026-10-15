using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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
        ILogger<ChatController> logger) : ControllerBase
    {
        private const string BadgeCacheKey = "chat_badges_v1";
        private static readonly TimeSpan BadgeCacheDuration = TimeSpan.FromMinutes(30);

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

            if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Invalid host");
            }

            var cacheKey = $"proxy_img_{url}";
            if (!memoryCache.TryGetValue(cacheKey, out (byte[] bytes, string contentType) cached))
            {
                try
                {
                    var client = httpClientFactory.CreateClient("Emotes");
                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                    request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");

                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    if (!response.IsSuccessStatusCode)
                    {
                        var safeUrl = SanitizeForLog(url);
                        logger.LogWarning("Upstream image fetch returned {StatusCode} for URL: {Url}", response.StatusCode, safeUrl);
                        return StatusCode((int)response.StatusCode);
                    }

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
                    var bytes = await response.Content.ReadAsByteArrayAsync();

                    if (bytes.Length <= 10 * 1024 * 1024)
                    {
                        cached = (bytes, contentType);
                        memoryCache.Set(cacheKey, cached, TimeSpan.FromHours(1));
                    }
                    else
                    {
                        return File(bytes, contentType);
                    }
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

        private static string SanitizeForLog(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sanitized = input.Replace("\r", string.Empty).Replace("\n", string.Empty);
            return sanitized.Length > 256 ? sanitized[..256] : sanitized;
        }
    }
}

