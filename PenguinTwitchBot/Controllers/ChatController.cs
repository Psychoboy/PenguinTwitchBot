using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Services;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace PenguinTwitchBot.Controllers
{
    /// <summary>
    /// Provides data for the chat overlay widget and local emote/image caching proxy.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController(
        ITwitchService twitchService,
        IMemoryCache memoryCache,
        IEmoteService emoteService,
        IHttpClientFactory httpClientFactory,
        ILogger<ChatController> logger,
        string? customCacheDirectory = null) : ControllerBase
    {
        private const string BadgeCacheKey = "chat_badges_v1";
        private static readonly TimeSpan BadgeCacheDuration = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan LocalEmoteCacheTtl = TimeSpan.FromHours(24);
        private static readonly SemaphoreSlim UpstreamThrottle = new(10, 10);
        private readonly string _diskCacheDir = !string.IsNullOrWhiteSpace(customCacheDirectory)
            ? customCacheDirectory
            : Path.Combine(AppContext.BaseDirectory, "data", "emote-cache");

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
        /// Proxies and locally caches remote images (e.g. Twitch, 7TV, BTTV, FFZ emotes and custom URLs)
        /// for 24 hours on disk + memory so overlays load locally without re-downloading every time.
        /// </summary>
        [HttpGet("proxy-image")]
        [ResponseCache(Duration = 86400)]
        public async Task<IActionResult> ProxyImage([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest("Invalid image URL");
            }

            if (IsLoopbackOrPrivateUri(uri))
            {
                return BadRequest("Invalid host");
            }

            var normalizedUrl = DropGameMediaResolver.NormalizeTwitchEmoteUrl(url);
            var cacheKey = $"proxy_img_{normalizedUrl}";

            if (memoryCache.TryGetValue(cacheKey, out (byte[] bytes, string contentType) cached) &&
                cached.bytes is { Length: > 0 })
            {
                return File(cached.bytes, cached.contentType);
            }

            if (TryReadFromDiskCache(normalizedUrl, out cached))
            {
                var memOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(LocalEmoteCacheTtl)
                    .SetSize(cached.bytes.Length);
                memoryCache.Set(cacheKey, cached, memOptions);
                return File(cached.bytes, cached.contentType);
            }

            try
            {
                var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
                var client = httpClientFactory.CreateClient("Emotes");
                using var request = new HttpRequestMessage(HttpMethod.Get, normalizedUrl);
                request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");

                await UpstreamThrottle.WaitAsync(cancellationToken);
                try
                {
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    // Manually follow a single redirect if the underlying handler did not auto-follow
                    if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400 && response.Headers.Location != null)
                    {
                        var redirectUri = response.Headers.Location.IsAbsoluteUri
                            ? response.Headers.Location
                            : new Uri(uri, response.Headers.Location);

                        if (IsLoopbackOrPrivateUri(redirectUri))
                        {
                            return BadRequest("Invalid redirect host");
                        }

                        using var redirectRequest = new HttpRequestMessage(HttpMethod.Get, redirectUri);
                        redirectRequest.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                        redirectRequest.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                        using var redirectResponse = await client.SendAsync(redirectRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        return await ProcessUpstreamResponseAsync(redirectResponse, normalizedUrl, cacheKey, cancellationToken);
                    }

                    return await ProcessUpstreamResponseAsync(response, normalizedUrl, cacheKey, cancellationToken);
                }
                finally
                {
                    UpstreamThrottle.Release();
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

        private async Task<IActionResult> ProcessUpstreamResponseAsync(
            HttpResponseMessage response,
            string normalizedUrl,
            string cacheKey,
            CancellationToken cancellationToken)
        {
            if (!response.IsSuccessStatusCode)
            {
                var safeUrl = SanitizeForLog(normalizedUrl);
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
            var cached = (bytes, contentType);
            var entryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(LocalEmoteCacheTtl)
                .SetSize(bytes.Length);
            memoryCache.Set(cacheKey, cached, entryOptions);
            WriteToDiskCache(normalizedUrl, bytes, contentType);

            return File(bytes, contentType);
        }

        private bool TryReadFromDiskCache(string url, out (byte[] bytes, string contentType) cached)
        {
            cached = default;
            try
            {
                var (binPath, metaPath) = GetDiskCachePaths(url);
                if (!System.IO.File.Exists(binPath) || !System.IO.File.Exists(metaPath))
                {
                    return false;
                }

                var age = DateTime.UtcNow - System.IO.File.GetLastWriteTimeUtc(binPath);
                if (age > LocalEmoteCacheTtl)
                {
                    return false;
                }

                var contentType = System.IO.File.ReadAllText(metaPath).Trim();
                if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var bytes = System.IO.File.ReadAllBytes(binPath);
                if (bytes.Length == 0)
                {
                    return false;
                }

                cached = (bytes, contentType);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to read emote disk cache for {Url}", SanitizeForLog(url));
                return false;
            }
        }

        private void WriteToDiskCache(string url, byte[] bytes, string contentType)
        {
            try
            {
                Directory.CreateDirectory(_diskCacheDir);
                var (binPath, metaPath) = GetDiskCachePaths(url);
                System.IO.File.WriteAllBytes(binPath, bytes);
                System.IO.File.WriteAllText(metaPath, contentType);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to write emote disk cache for {Url}", SanitizeForLog(url));
            }
        }

        private (string binPath, string metaPath) GetDiskCachePaths(string url)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
            var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            return (
                Path.Combine(_diskCacheDir, $"{hashHex}.bin"),
                Path.Combine(_diskCacheDir, $"{hashHex}.meta")
            );
        }

        private static bool IsLoopbackOrPrivateUri(Uri uri)
        {
            if (uri.IsLoopback ||
                uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (IPAddress.TryParse(uri.Host, out var ip))
            {
                if (IPAddress.IsLoopback(ip)) return true;
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    var b = ip.GetAddressBytes();
                    // 10.0.0.0/8, 127.0.0.0/8, 169.254.0.0/16, 172.16.0.0/12, 192.168.0.0/16, 0.0.0.0/8
                    if (b[0] == 10 || b[0] == 127 || b[0] == 0 ||
                        (b[0] == 169 && b[1] == 254) ||
                        (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                        (b[0] == 192 && b[1] == 168))
                    {
                        return true;
                    }
                }
                else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.Equals(IPAddress.IPv6None))
                    {
                        return true;
                    }
                    var b = ip.GetAddressBytes();
                    // fc00::/7 (Unique Local)
                    if ((b[0] & 0xfe) == 0xfc)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string SanitizeForLog(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sanitized = input.Replace("\r", string.Empty).Replace("\n", string.Empty);
            return sanitized.Length > 256 ? sanitized[..256] : sanitized;
        }
    }
}

