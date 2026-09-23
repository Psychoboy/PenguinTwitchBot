using System.Net;
using System.Text;
using NSubstitute;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public class IntegrationTestServiceTests
{
    private sealed class DelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    [Fact]
    public async Task TestYoutubeAsync_EmptyKey_ReturnsBadRequest()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var service = new IntegrationTestService(httpClientFactory);

        var result = await service.TestYoutubeAsync("");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestDiscordAsync_EmptyToken_ReturnsBadRequest()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var service = new IntegrationTestService(httpClientFactory);

        var result = await service.TestDiscordAsync(null);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestWeatherAsync_EmptyKey_ReturnsBadRequest()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var service = new IntegrationTestService(httpClientFactory);

        var result = await service.TestWeatherAsync("   ", "London");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestOpenAiAsync_EmptyKey_ReturnsBadRequest()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var service = new IntegrationTestService(httpClientFactory);

        var result = await service.TestOpenAiAsync("");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestDiscordAsync_Success_ParsesBotInfo()
    {
        var handler = new DelegatingHandlerStub(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/users/@me/guilds"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\":\"123\",\"name\":\"Test Server\"}]", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"987654321\",\"username\":\"TestBot\"}", Encoding.UTF8, "application/json")
            };
        });

        var client = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);

        var service = new IntegrationTestService(httpClientFactory);
        var result = await service.TestDiscordAsync("valid-fake-token");

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("TestBot", result.Details);
        Assert.Contains("1 server(s)", result.Details);
    }

    [Fact]
    public async Task TestDiscordAsync_Unauthorized_Returns401AndDiagnostic()
    {
        var handler = new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"message\":\"401: Unauthorized\",\"code\":0}", Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);

        var service = new IntegrationTestService(httpClientFactory);
        var result = await service.TestDiscordAsync("bad-token");

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.Contains("Unauthorized", result.ErrorMessage);
    }

    [Fact]
    public async Task TestWeatherAsync_Success_ParsesWeatherDetails()
    {
        var json = """
        {
            "location": { "name": "Phoenix", "country": "United States of America" },
            "current": { "temp_f": 78.5, "temp_c": 25.8, "humidity": 30, "condition": { "text": "Sunny" } }
        }
        """;

        var handler = new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);

        var service = new IntegrationTestService(httpClientFactory);
        var result = await service.TestWeatherAsync("test-key", "Phoenix");

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("Phoenix", result.Details);
        Assert.Contains("Sunny", result.Details);
        Assert.Contains("78.5", result.Details);
    }

    [Fact]
    public async Task TestWeatherAsync_ApiError_ParsesErrorMessage()
    {
        var errorJson = """
        {
            "error": { "code": 2006, "message": "API key is invalid." }
        }
        """;

        var handler = new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(errorJson, Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);

        var service = new IntegrationTestService(httpClientFactory);
        var result = await service.TestWeatherAsync("invalid-key", "London");

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("API key is invalid.", result.ErrorMessage);
    }

    [Fact]
    public async Task TestOpenAiAsync_Success_ParsesModelsCount()
    {
        var json = """
        {
            "data": [
                { "id": "gpt-4o" },
                { "id": "gpt-4o-mini" },
                { "id": "gpt-5.1" }
            ]
        }
        """;

        var handler = new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);

        var service = new IntegrationTestService(httpClientFactory);
        var result = await service.TestOpenAiAsync("sk-fake-key");

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("3 models", result.Details);
    }

    [Fact]
    public async Task TestOpenAiAsync_InvalidKey_ParsesError()
    {
        var errorJson = """
        {
            "error": {
                "message": "Incorrect API key provided: sk-bad***.",
                "type": "invalid_request_error",
                "code": "invalid_api_key"
            }
        }
        """;

        var handler = new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(errorJson, Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);

        var service = new IntegrationTestService(httpClientFactory);
        var result = await service.TestOpenAiAsync("sk-bad");

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.Contains("Incorrect API key provided", result.ErrorMessage);
    }
}

