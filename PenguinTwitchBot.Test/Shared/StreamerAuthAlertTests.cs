using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using NSubstitute;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Circuit;
using PenguinTwitchBot.CustomMiddleware;
using PenguinTwitchBot.Services;
using PenguinTwitchBot.Shared;
using Xunit;

namespace PenguinTwitchBot.Test.Shared;

public class StreamerAuthAlertTests
{
    private readonly ITwitchService _twitchService = Substitute.For<ITwitchService>();
    private readonly ICircuitUserService _circuitUserService = Substitute.For<ICircuitUserService>();
    private readonly IServiceBackbone _serviceBackbone = Substitute.For<IServiceBackbone>();
    private readonly IVersionCheckService _versionCheckService = Substitute.For<IVersionCheckService>();
    private readonly IFeatureRuntimeCoordinator _featureCoordinator = Substitute.For<IFeatureRuntimeCoordinator>();
    private readonly ILogger<MainLayout> _logger = Substitute.For<ILogger<MainLayout>>();
    private readonly ILogger<CircuitHandlerService> _circuitLogger = Substitute.For<ILogger<CircuitHandlerService>>();

    private (BunitContext ctx, Bunit.TestDoubles.BunitAuthorizationContext auth) SetupServices()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        var auth = ctx.AddAuthorization();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationTitle"] = "Penguin Twitch Bot",
            ["Secrets:SecretsConf"] = "secrets.json"
        }).Build();
        ctx.Services.AddSingleton<IConfiguration>(config);

        ctx.Services.AddSingleton(_twitchService);
        ctx.Services.AddSingleton(_circuitUserService);
        ctx.Services.AddSingleton(_serviceBackbone);
        ctx.Services.AddSingleton(_versionCheckService);
        ctx.Services.AddSingleton(_featureCoordinator);
        ctx.Services.AddSingleton(_logger);
        ctx.Services.AddScoped<BlazorAppContext>();

        var userThemeService = Substitute.For<IUserThemeService>();
        userThemeService.CurrentTheme.Returns(new MudBlazor.MudTheme());
        userThemeService.AvailableThemes.Returns(new List<PenguinTwitchBot.Models.Themes.CustomThemeModel>());
        ctx.Services.AddSingleton(userThemeService);

        var circuitHandler = new CircuitHandlerService(_circuitUserService, _circuitLogger);
        ctx.Services.AddSingleton<CircuitHandler>(circuitHandler);

        return (ctx, auth);
    }

    [Fact]
    public async Task WhenStreamerLoggedIn_AndTwitchServiceAuthenticationDisconnected_AlertIsDisplayed()
    {
        var (ctx, auth) = SetupServices();
        auth.SetAuthorized("StreamerUser");
        auth.SetRoles("Streamer");

        await using (ctx)
        {
            _twitchService.GetStatus().Returns(TwitchServiceStatus.AuthenticationDisconnected);

            var cut = ctx.Render<MainLayout>();

            var alertElements = cut.FindAll(".streamer-auth-alert");
            Assert.NotEmpty(alertElements);
            Assert.Contains("STREAMER ACCOUNT NOT CONNECTED", cut.Markup);
            Assert.Contains("Connect Now", cut.Markup);
            Assert.Contains("/settings/bot-auth", cut.Markup);
        }
    }

    [Fact]
    public async Task WhenStreamerLoggedIn_AndTwitchServiceUnavailable_AlertIsDisplayedWithoutConnectButton()
    {
        var (ctx, auth) = SetupServices();
        auth.SetAuthorized("StreamerUser");
        auth.SetRoles("Streamer");

        await using (ctx)
        {
            _twitchService.GetStatus().Returns(TwitchServiceStatus.Unavailable);

            var cut = ctx.Render<MainLayout>();

            var alertElements = cut.FindAll(".streamer-unavailable-alert");
            Assert.NotEmpty(alertElements);
            Assert.Contains("TWITCH SERVICE UNAVAILABLE", cut.Markup);
            Assert.DoesNotContain("Connect Now", cut.Markup);
            Assert.Empty(cut.FindAll(".streamer-auth-alert"));
        }
    }

    [Fact]
    public async Task WhenStreamerLoggedIn_AndTwitchServiceConnected_AlertIsNotDisplayed()
    {
        var (ctx, auth) = SetupServices();
        auth.SetAuthorized("StreamerUser");
        auth.SetRoles("Streamer");

        await using (ctx)
        {
            _twitchService.GetStatus().Returns(TwitchServiceStatus.Connected);

            var cut = ctx.Render<MainLayout>();

            Assert.Empty(cut.FindAll(".streamer-auth-alert"));
            Assert.Empty(cut.FindAll(".streamer-unavailable-alert"));
            Assert.DoesNotContain("STREAMER ACCOUNT NOT CONNECTED", cut.Markup);
            Assert.DoesNotContain("TWITCH SERVICE UNAVAILABLE", cut.Markup);
            Assert.DoesNotContain("Connect Now", cut.Markup);
        }
    }

    [Fact]
    public async Task WhenStreamerLoggedIn_AndTwitchServiceUnknown_AlertIsNotDisplayed()
    {
        var (ctx, auth) = SetupServices();
        auth.SetAuthorized("StreamerUser");
        auth.SetRoles("Streamer");

        await using (ctx)
        {
            _twitchService.GetStatus().Returns(TwitchServiceStatus.Unknown);

            var cut = ctx.Render<MainLayout>();

            Assert.Empty(cut.FindAll(".streamer-auth-alert"));
            Assert.Empty(cut.FindAll(".streamer-unavailable-alert"));
            Assert.DoesNotContain("STREAMER ACCOUNT NOT CONNECTED", cut.Markup);
            Assert.DoesNotContain("TWITCH SERVICE UNAVAILABLE", cut.Markup);
            Assert.DoesNotContain("Connect Now", cut.Markup);
        }
    }

    [Fact]
    public async Task WhenViewerLoggedIn_AndTwitchServiceAuthenticationDisconnected_AlertIsNotDisplayed()
    {
        var (ctx, auth) = SetupServices();
        auth.SetAuthorized("ViewerUser");
        auth.SetRoles("Viewer");

        await using (ctx)
        {
            _twitchService.GetStatus().Returns(TwitchServiceStatus.AuthenticationDisconnected);

            var cut = ctx.Render<MainLayout>();

            Assert.Empty(cut.FindAll(".streamer-auth-alert"));
            Assert.Empty(cut.FindAll(".streamer-unavailable-alert"));
            Assert.DoesNotContain("STREAMER ACCOUNT NOT CONNECTED", cut.Markup);
            Assert.DoesNotContain("Connect Now", cut.Markup);
        }
    }

    [Fact]
    public async Task WhenAnonymous_AndTwitchServiceAuthenticationDisconnected_AlertIsNotDisplayed()
    {
        var (ctx, auth) = SetupServices();
        auth.SetNotAuthorized();

        await using (ctx)
        {
            _twitchService.GetStatus().Returns(TwitchServiceStatus.AuthenticationDisconnected);

            var cut = ctx.Render<MainLayout>();

            Assert.Empty(cut.FindAll(".streamer-auth-alert"));
            Assert.Empty(cut.FindAll(".streamer-unavailable-alert"));
            Assert.DoesNotContain("STREAMER ACCOUNT NOT CONNECTED", cut.Markup);
            Assert.DoesNotContain("Connect Now", cut.Markup);
        }
    }

    [Fact]
    public async Task WhenServiceResolves_ServiceStatusChangedHidesAlert()
    {
        var (ctx, auth) = SetupServices();
        auth.SetAuthorized("StreamerUser");
        auth.SetRoles("Streamer");

        await using (ctx)
        {
            _twitchService.GetStatus().Returns(TwitchServiceStatus.AuthenticationDisconnected);

            var cut = ctx.Render<MainLayout>();

            Assert.NotEmpty(cut.FindAll(".streamer-auth-alert"));
            Assert.Contains("STREAMER ACCOUNT NOT CONNECTED", cut.Markup);

            // Now simulate service resolving
            _twitchService.GetStatus().Returns(TwitchServiceStatus.Connected);
            await cut.InvokeAsync(() =>
            {
                _twitchService.ServiceStatusChanged += Raise.Event<EventHandler<TwitchServiceStatus>>(_twitchService, TwitchServiceStatus.Connected);
            });

            Assert.Empty(cut.FindAll(".streamer-auth-alert"));
            Assert.DoesNotContain("STREAMER ACCOUNT NOT CONNECTED", cut.Markup);
        }
    }
}
