using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using NSubstitute;
using PenguinTwitchBot.Models.Themes;
using PenguinTwitchBot.Services;
using Xunit;
using DbThemePreference = PenguinTwitchBot.Database.Bot.Models.Themes.UserThemePreference;
using UserThemePreference = PenguinTwitchBot.Models.Themes.UserThemePreference;

namespace PenguinTwitchBot.Test.Services;

public class UserThemeServiceTests
{
    private readonly ICustomThemeService _customThemeService;
    private readonly IJSRuntime _jsRuntime;
    private readonly List<CustomThemeModel> _themes;

    public UserThemeServiceTests()
    {
        _customThemeService = Substitute.For<ICustomThemeService>();
        _jsRuntime = Substitute.For<IJSRuntime>();

        _themes = PresetThemes.GetPresets();
        _customThemeService.GetThemesAsync().Returns(Task.FromResult(_themes));
        _customThemeService.GetCachedThemes().Returns(_themes);
        _customThemeService.GetDefaultTheme().Returns(_themes.First(x => x.IsDefault));
    }

    [Fact]
    public async Task InitializeAsync_LoadsPreferenceFromLocalStorage()
    {
        var savedPreference = new UserThemePreference
        {
            IsDarkMode = false,
            ThemeId = PresetThemes.ArcticThemeId
        };

        _jsRuntime.InvokeAsync<UserThemePreference?>("penguinTheme.getPreference", Arg.Any<object?[]>())
            .Returns(new ValueTask<UserThemePreference?>(savedPreference));

        var service = new UserThemeService(_customThemeService);
        bool eventFired = false;
        service.OnThemeChanged += () => eventFired = true;

        await service.InitializeAsync(_jsRuntime);

        Assert.True(service.IsInitialized);
        Assert.False(service.IsDarkMode);
        Assert.Equal(PresetThemes.ArcticThemeId, service.CurrentThemeId);
        Assert.True(eventFired);
    }

    [Fact]
    public async Task InitializeAsync_DefaultsToDarkModeAndDefaultTheme_WhenNoPreference()
    {
        _jsRuntime.InvokeAsync<UserThemePreference?>("penguinTheme.getPreference", Arg.Any<object?[]>())
            .Returns(new ValueTask<UserThemePreference?>((UserThemePreference?)null));

        var service = new UserThemeService(_customThemeService);
        await service.InitializeAsync(_jsRuntime);

        Assert.True(service.IsInitialized);
        Assert.True(service.IsDarkMode);
        Assert.Equal(PresetThemes.DefaultThemeId, service.CurrentThemeId);
    }

    [Fact]
    public async Task ToggleDarkModeAsync_TogglesMode_AndSavesPreference()
    {
        var service = new UserThemeService(_customThemeService);
        await service.InitializeAsync(_jsRuntime);

        bool initialMode = service.IsDarkMode;
        bool eventFired = false;
        service.OnThemeChanged += () => eventFired = true;

        await service.ToggleDarkModeAsync(_jsRuntime);

        Assert.Equal(!initialMode, service.IsDarkMode);
        Assert.True(eventFired);

        await _jsRuntime.Received(1).InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "penguinTheme.setPreference",
            Arg.Is<object?[]>(args => (bool)args[0]! == !initialMode && (string)args[1]! == service.CurrentThemeId));
    }

    [Fact]
    public async Task SelectThemeAsync_UpdatesCurrentTheme_AndSavesPreference()
    {
        var service = new UserThemeService(_customThemeService);
        await service.InitializeAsync(_jsRuntime);

        bool eventFired = false;
        service.OnThemeChanged += () => eventFired = true;

        await service.SelectThemeAsync(PresetThemes.MidnightPurpleThemeId, _jsRuntime);

        Assert.Equal(PresetThemes.MidnightPurpleThemeId, service.CurrentThemeId);
        Assert.True(eventFired);

        await _jsRuntime.Received(1).InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "penguinTheme.setPreference",
            Arg.Is<object?[]>(args => (string)args[1]! == PresetThemes.MidnightPurpleThemeId));
    }

    [Fact]
    public async Task InitializeAsync_ExcludesDisabledThemes_AndFallsBackIfSavedThemeIsDisabled()
    {
        // Disable arctic theme
        var arctic = _themes.First(t => t.Id == PresetThemes.ArcticThemeId);
        arctic.IsEnabled = false;

        var savedPreference = new UserThemePreference
        {
            IsDarkMode = false,
            ThemeId = PresetThemes.ArcticThemeId
        };

        _jsRuntime.InvokeAsync<UserThemePreference?>("penguinTheme.getPreference", Arg.Any<object?[]>())
            .Returns(new ValueTask<UserThemePreference?>(savedPreference));

        var service = new UserThemeService(_customThemeService);
        await service.InitializeAsync(_jsRuntime);

        Assert.DoesNotContain(service.AvailableThemes, t => t.Id == PresetThemes.ArcticThemeId);
        // Should fall back to default theme since Arctic is disabled
        Assert.Equal(PresetThemes.DefaultThemeId, service.CurrentThemeId);
    }

    [Fact]
    public async Task RefreshThemesAsync_RevertsToDefault_WhenCurrentThemeIsDisabled()
    {
        var service = new UserThemeService(_customThemeService);
        await service.InitializeAsync(_jsRuntime);

        await service.SelectThemeAsync(PresetThemes.CyberpunkThemeId, _jsRuntime);
        Assert.Equal(PresetThemes.CyberpunkThemeId, service.CurrentThemeId);

        // Cyberpunk is disabled by streamer
        var cyberpunk = _themes.First(t => t.Id == PresetThemes.CyberpunkThemeId);
        cyberpunk.IsEnabled = false;

        await service.RefreshThemesAsync();

        Assert.Equal(PresetThemes.DefaultThemeId, service.CurrentThemeId);
        Assert.DoesNotContain(service.AvailableThemes, t => t.Id == PresetThemes.CyberpunkThemeId);
    }

    [Fact]
    public async Task SelectThemeAsync_ReturnsTrueWhenFound_AndFalseWhenFallbackToDefault()
    {
        var service = new UserThemeService(_customThemeService);
        await service.InitializeAsync(_jsRuntime);

        var success = await service.SelectThemeAsync(PresetThemes.MidnightPurpleThemeId, _jsRuntime);
        Assert.True(success);
        Assert.Equal(PresetThemes.MidnightPurpleThemeId, service.CurrentThemeId);

        var failed = await service.SelectThemeAsync("non-existent-theme-id", _jsRuntime);
        Assert.False(failed);
        Assert.Equal(PresetThemes.DefaultThemeId, service.CurrentThemeId);
    }

    [Fact]
    public void Constructor_ImmediatelyInitializesWithDefaultTheme_BeforeAsyncOrJsRuns()
    {
        var arcticTheme = _themes.First(x => x.Id == PresetThemes.ArcticThemeId);
        _customThemeService.GetDefaultTheme().Returns(arcticTheme);

        var service = new UserThemeService(_customThemeService);

        Assert.Equal(PresetThemes.ArcticThemeId, service.CurrentThemeId);
        Assert.NotNull(arcticTheme.DarkPalette.Primary);
        Assert.Equal(arcticTheme.DarkPalette.Primary.ToLowerInvariant(), ThemePaletteModel.ColorToString(service.CurrentTheme.PaletteDark.Primary).ToLowerInvariant());
    }

    [Fact]
    public void ApplyInitialPreference_UpdatesThemeAndDarkMode()
    {
        var service = new UserThemeService(_customThemeService);
        var prefJson = "{\"isDarkMode\":false,\"themeId\":\"" + PresetThemes.CyberpunkThemeId + "\"}";

        service.ApplyInitialPreference(prefJson);

        Assert.False(service.IsDarkMode);
        Assert.Equal(PresetThemes.CyberpunkThemeId, service.CurrentThemeId);
    }

    [Fact]
    public async Task InitializeAsync_WhenAuthenticatedAndNoDbRecord_SavesLocalPrefToDb()
    {
        var prefService = Substitute.For<IUserThemePreferenceService>();
        var metricsService = Substitute.For<IThemeMetricsService>();
        var authProvider = Substitute.For<AuthenticationStateProvider>();

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("UserId", "user-123")], "TestAuth"));
        authProvider.GetAuthenticationStateAsync().Returns(Task.FromResult(new AuthenticationState(user)));
        prefService.GetPreferenceAsync("user-123").Returns(Task.FromResult<DbThemePreference?>(null));

        var savedPreference = new UserThemePreference
        {
            IsDarkMode = false,
            ThemeId = PresetThemes.ArcticThemeId
        };
        _jsRuntime.InvokeAsync<UserThemePreference?>("penguinTheme.getPreference", Arg.Any<object?[]>())
            .Returns(new ValueTask<UserThemePreference?>(savedPreference));

        var service = new UserThemeService(_customThemeService, null, prefService, metricsService, authProvider);
        await service.InitializeAsync(_jsRuntime);

        await prefService.Received(1).SavePreferenceAsync("user-123", PresetThemes.ArcticThemeId, false);
        metricsService.Received().TrackActiveSession(Arg.Any<string>(), PresetThemes.ArcticThemeId, false);
    }

    [Fact]
    public async Task InitializeAsync_WhenAuthenticatedAndDbRecordDiffers_UpdatesLocalFromDb()
    {
        var prefService = Substitute.For<IUserThemePreferenceService>();
        var metricsService = Substitute.For<IThemeMetricsService>();
        var authProvider = Substitute.For<AuthenticationStateProvider>();

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("UserId", "user-456")], "TestAuth"));
        authProvider.GetAuthenticationStateAsync().Returns(Task.FromResult(new AuthenticationState(user)));

        // Local cache has Arctic light mode
        var localPref = new UserThemePreference
        {
            IsDarkMode = false,
            ThemeId = PresetThemes.ArcticThemeId
        };
        _jsRuntime.InvokeAsync<UserThemePreference?>("penguinTheme.getPreference", Arg.Any<object?[]>())
            .Returns(new ValueTask<UserThemePreference?>(localPref));

        // DB has Cyberpunk dark mode
        var dbPref = new DbThemePreference
        {
            UserId = "user-456",
            ThemeId = PresetThemes.CyberpunkThemeId,
            IsDarkMode = true
        };
        prefService.GetPreferenceAsync("user-456").Returns(Task.FromResult<DbThemePreference?>(dbPref));

        var service = new UserThemeService(_customThemeService, null, prefService, metricsService, authProvider);
        await service.InitializeAsync(_jsRuntime);

        // Should have updated to Cyberpunk dark mode from DB
        Assert.Equal(PresetThemes.CyberpunkThemeId, service.CurrentThemeId);
        Assert.True(service.IsDarkMode);

        // Should have synchronized to JS
        await _jsRuntime.Received().InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "penguinTheme.setPreference",
            Arg.Is<object?[]>(args => (bool)args[0]! == true && (string)args[1]! == PresetThemes.CyberpunkThemeId));
    }

    [Fact]
    public async Task SelectThemeAsync_WhenAuthenticated_SavesToDbAndTracksActive()
    {
        var prefService = Substitute.For<IUserThemePreferenceService>();
        var metricsService = Substitute.For<IThemeMetricsService>();
        var authProvider = Substitute.For<AuthenticationStateProvider>();

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("UserId", "user-789")], "TestAuth"));
        authProvider.GetAuthenticationStateAsync().Returns(Task.FromResult(new AuthenticationState(user)));

        var service = new UserThemeService(_customThemeService, null, prefService, metricsService, authProvider);
        await service.InitializeAsync(_jsRuntime);

        await service.SelectThemeAsync(PresetThemes.MidnightPurpleThemeId, _jsRuntime);

        await prefService.Received().SavePreferenceAsync("user-789", PresetThemes.MidnightPurpleThemeId, true);
        metricsService.Received().TrackActiveSession(Arg.Any<string>(), PresetThemes.MidnightPurpleThemeId, true);
    }
}
