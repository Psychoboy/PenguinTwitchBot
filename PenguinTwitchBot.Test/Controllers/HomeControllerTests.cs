using System.Security.Claims;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Models;
using PenguinTwitchBot.TwitchApi.Auth;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NSubstitute;

namespace PenguinTwitchBot.Test.Controllers;

public class HomeControllerTests
{
    [Fact]
    public void Signin_ShouldReturnTwitchAuthorizeRedirect()
    {
        var (sut, _, _, _) = CreateSut();

        var result = sut.Signin("/dashboard");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("https://id.twitch.tv/oauth2/authorize?", redirect.Url);
        Assert.Contains("response_type=code", redirect.Url, StringComparison.Ordinal);
        Assert.Contains("client_id=test-client", redirect.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OAuthRedirect_ShouldReturnRoot_WhenStateIsMissing()
    {
        var (sut, _, _, _) = CreateSut();

        var result = await sut.OAuthRedirect("test-code", "missing-state", null);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirect.Url);
    }

    [Fact]
    public async Task OAuthRedirect_ShouldSignInAndRedirect_WhenStateAndUserAreValid()
    {
        var (sut, authClient, viewerFeature, fakeAuthService) = CreateSut();

        var state = "valid-state";
        SetState(sut, state, "/dashboard");

        authClient.ExchangeCodeAsync("test-client", "test-secret", "test-code", "http://localhost/redirect")
            .Returns(new TwitchAuthTokenResponse
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                ExpiresIn = 3600
            });

        authClient.GetAuthenticatedUserAsync("test-client", "access-token")
            .Returns(new TwitchAuthenticatedUser
            {
                Id = "1",
                Login = "streamer",
                DisplayName = "Streamer",
                ProfileImageUrl = "https://cdn.example/streamer.png"
            });

        viewerFeature.IsFollowerByUsername("streamer").Returns(true);
        viewerFeature.GetViewerByUserId("1").Returns(new Viewer
        {
            isMod = true,
            isSub = true,
            isVip = true,
            isEditor = true,
        });

        var result = await sut.OAuthRedirect("test-code", state, null);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/dashboard", redirect.Url);

        Assert.NotNull(fakeAuthService.LastPrincipal);
        var claims = fakeAuthService.LastPrincipal!.Claims.ToList();

        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "streamer");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Streamer");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Follower");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Moderator");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Subscriber");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "VIP");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Editor");
    }

    private static void SetState(HomeController controller, string state, string redirect)
    {
        var memoryCache = controller.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        memoryCache.Set(state, new PenguinTwitchBot.Controllers.HomeController.OAuthStateEntry(
            PenguinTwitchBot.Controllers.HomeController.OAuthIntent.User, redirect));
    }

    private static (HomeController sut, IAuthClient authClient, IViewerFeature viewerFeature, FakeAuthenticationService fakeAuthService) CreateSut()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["twitchClientId"] = "test-client",
                ["twitchClientSecret"] = "test-secret",
                ["broadcaster"] = "streamer",
                ["botName"] = "bot"
            })
            .Build();

        var logger = Substitute.For<ILogger<HomeController>>();
        var settingsManager = new SettingsFileManager(Substitute.For<ILogger<SettingsFileManager>>(), config);
        var viewerFeature = Substitute.For<IViewerFeature>();
        var twitchChatBot = Substitute.For<ITwitchChatBot>();
        var twitchService = Substitute.For<ITwitchService>();
        var authClient = Substitute.For<IAuthClient>();

        var fakeAuthService = new FakeAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<IAuthenticationService>(fakeAuthService);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");

        var sut = new HomeController(config, logger, settingsManager, viewerFeature, provider.GetRequiredService<IMemoryCache>(), twitchChatBot, twitchService, authClient)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        var url = new Mock<IUrlHelper>();
        url.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns<string>(s => !string.IsNullOrWhiteSpace(s) && s.StartsWith('/'));
        sut.Url = url.Object;

        return (sut, authClient, viewerFeature, fakeAuthService);
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public ClaimsPrincipal? LastPrincipal { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            LastPrincipal = principal;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }
    }
}
