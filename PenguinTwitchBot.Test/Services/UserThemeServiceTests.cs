using Microsoft.JSInterop;
using NSubstitute;
using PenguinTwitchBot.Models.Themes;
using PenguinTwitchBot.Services;
using Xunit;

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
}
