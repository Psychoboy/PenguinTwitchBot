using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public class EmoteServiceTests
{
    private readonly Mock<ITwitchService> _twitchServiceMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<ILogger<EmoteService>> _loggerMock = new();
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

    private EmoteService CreateService(HttpMessageHandler? handler = null)
    {
        handler ??= new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        _httpClientFactoryMock.Setup(f => f.CreateClient("Emotes")).Returns(httpClient);

        return new EmoteService(
            _twitchServiceMock.Object,
            _memoryCache,
            _httpClientFactoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ResolveEmoteUrlAsync_ReturnsCachedEmote_WithoutUpstreamFetch()
    {
        var service = CreateService();

        // Seed cache directly
        var cached = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Kappa"] = "https://static-cdn.jtvnw.net/emoticons/v2/25/default/dark/2.0"
        };
        _memoryCache.Set("chat_emotes_v1", cached, TimeSpan.FromMinutes(10));

        var url = await service.ResolveEmoteUrlAsync("Kappa");

        Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/25/default/dark/2.0", url);
        _twitchServiceMock.Verify(t => t.GetChatEmotesAsync(), Times.Never);
    }

    [Fact]
    public async Task ResolveEmoteUrlAsync_OnForcedRefresh_DoesNotDeadlockAndReturnsNewEmote()
    {
        // Initial state: broadcaster user ID is known
        _twitchServiceMock.Setup(t => t.GetBroadcasterUserId()).ReturnsAsync("123456");
        _twitchServiceMock.Setup(t => t.GetChatEmotesAsync()).ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sptvShots"] = "https://static-cdn.jtvnw.net/emoticons/v2/sptv123/default/dark/2.0"
        });

        var service = CreateService();

        // Initially cached without the newly created emote
        var cached = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ExistingEmote"] = "https://static-cdn.jtvnw.net/emoticons/v2/1/default/dark/2.0"
        };
        // Simulated as cached without channel ID to trigger forced refresh on miss
        _memoryCache.Set("chat_emotes_v1", cached, TimeSpan.FromMinutes(10));

        // Attempting to resolve "sptvShots" will trigger the refresh decision, invalidating cache,
        // releasing semaphore, calling GetEmotesAsync without deadlock, and finding the new emote.
        var resolveTask = service.ResolveEmoteUrlAsync("sptvShots");
        var completedTask = await Task.WhenAny(resolveTask, Task.Delay(5000));

        Assert.Same(resolveTask, completedTask); // Must finish within 5s, confirming no deadlock!
        var url = await resolveTask;
        Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/sptv123/default/dark/2.0", url);
    }

    [Fact]
    public async Task TrackEmote_ResolvesImmediately_WithoutUpstreamFetch()
    {
        var service = CreateService();

        service.TrackEmote("sptvChatEmote", "https://static-cdn.jtvnw.net/emoticons/v2/chat123/default/dark/3.0");

        var url = await service.ResolveEmoteUrlAsync("sptvChatEmote");

        Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/chat123/default/dark/3.0", url);
        _twitchServiceMock.Verify(t => t.GetChatEmotesAsync(), Times.Never);

        var map = await service.GetEmotesAsync();
        Assert.True(map.ContainsKey("sptvChatEmote"));
        Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/chat123/default/dark/3.0", map["sptvChatEmote"]);
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}

