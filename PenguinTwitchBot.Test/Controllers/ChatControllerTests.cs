using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Controllers;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Controllers
{
    public class ChatControllerTests
    {
        private readonly Mock<ITwitchService> _twitchServiceMock = new();
        private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());
        private readonly Mock<IEmoteService> _emoteServiceMock = new();
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
        private readonly Mock<ILogger<ChatController>> _loggerMock = new();
        private readonly Mock<IDropRegistry> _dropRegistryMock = new();
        private readonly Mock<IDropGamesSettingsService> _settingsServiceMock = new();

        public ChatControllerTests()
        {
            _dropRegistryMock.Setup(r => r.IsProxyUrlAllowed(It.IsAny<string>())).Returns(true);
            _settingsServiceMock.Setup(s => s.GetSettingsAsync()).ReturnsAsync(new DropGamesSettings());
        }

        private ChatController CreateController()
        {
            return new ChatController(
                _twitchServiceMock.Object,
                _memoryCache,
                _emoteServiceMock.Object,
                _httpClientFactoryMock.Object,
                _loggerMock.Object);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-url")]
        [InlineData("ftp://example.com/image.png")]
        [InlineData("file:///etc/passwd")]
        public async Task ProxyImage_InvalidOrNonHttpUrl_ReturnsBadRequest(string url)
        {
            var controller = CreateController();

            var result = await controller.ProxyImage(url);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [InlineData("http://localhost/image.png")]
        [InlineData("http://127.0.0.1/image.png")]
        [InlineData("https://localhost:5000/image.png")]
        public async Task ProxyImage_LoopbackOrLocalhost_ReturnsBadRequest(string url)
        {
            var controller = CreateController();

            var result = await controller.ProxyImage(url);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ProxyImage_ValidRemoteImage_FetchesAndCachesResponse()
        {
            var testUrl = $"https://static-cdn.jtvnw.net/emoticons/v2/emotesv2_{Guid.NewGuid():N}/default/dark/2.0";
            var fakeBytes = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }; // GIF89a

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString() == testUrl),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(fakeBytes)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/gif") }
                    }
                });

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient("Emotes")).Returns(httpClient);

            var controller = CreateController();

            // First call fetches from HTTP client
            var result = await controller.ProxyImage(testUrl);
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("image/gif", fileResult.ContentType);
            Assert.Equal(fakeBytes, fileResult.FileContents);

            // Second call should serve from cache (handler called only once)
            var cachedResult = await controller.ProxyImage(testUrl);
            var cachedFileResult = Assert.IsType<FileContentResult>(cachedResult);
            Assert.Equal(fakeBytes, cachedFileResult.FileContents);

            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task ProxyImage_UpstreamNotFound_ReturnsStatusCode()
        {
            var testUrl = $"https://static-cdn.jtvnw.net/emoticons/v2/missing_{Guid.NewGuid():N}/default/dark/2.0";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound
                });

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient("Emotes")).Returns(httpClient);

            var controller = CreateController();

            var result = await controller.ProxyImage(testUrl);
            var statusResult = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(404, statusResult.StatusCode);
        }

        [Fact]
        public async Task ProxyImage_ExternalEmoteHost_FetchesAndPersistsToDiskCacheAcrossMemoryResets()
        {
            var testUrl = $"https://custom-emotes.example.com/special-event/{Guid.NewGuid():N}.png";
            var fakeBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString() == testUrl),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(fakeBytes)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png") }
                    }
                });

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient("Emotes")).Returns(httpClient);

            var controller1 = CreateController();
            var result1 = await controller1.ProxyImage(testUrl);
            var fileResult1 = Assert.IsType<FileContentResult>(result1);
            Assert.Equal("image/png", fileResult1.ContentType);
            Assert.Equal(fakeBytes, fileResult1.FileContents);

            // Create a second controller with a brand-new empty MemoryCache to verify 24h disk cache persistence
            using var freshMemoryCache = new MemoryCache(new MemoryCacheOptions());
            var controller2 = new ChatController(
                _twitchServiceMock.Object,
                freshMemoryCache,
                _emoteServiceMock.Object,
                _httpClientFactoryMock.Object,
                _loggerMock.Object);

            var result2 = await controller2.ProxyImage(testUrl);
            var fileResult2 = Assert.IsType<FileContentResult>(result2);
            Assert.Equal("image/png", fileResult2.ContentType);
            Assert.Equal(fakeBytes, fileResult2.FileContents);

            // Upstream HTTP handler was only invoked once
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task ProxyImage_NonImageContentType_ReturnsBadRequest()
        {
            var testUrl = $"https://static-cdn.jtvnw.net/emoticons/v2/test_{Guid.NewGuid():N}/default/dark/2.0";
            var fakeBytes = "<html><body>Not an image</body></html>"u8.ToArray();

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(fakeBytes)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html") }
                    }
                });

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient("Emotes")).Returns(httpClient);

            var controller = CreateController();
            var result = await controller.ProxyImage(testUrl);
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}

