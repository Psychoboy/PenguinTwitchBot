using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Database.Bot.Models.Themes;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public class UserThemePreferenceServiceTests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserThemePreferenceService> _logger;
    private readonly UserThemePreferenceService _service;

    public UserThemePreferenceServiceTests()
    {
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scope = Substitute.For<IServiceScope>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _scopeFactory.CreateScope().Returns(_scope);
        _scope.ServiceProvider.Returns(_serviceProvider);
        _serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);

        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<UserThemePreferenceService>>();
        _service = new UserThemePreferenceService(_scopeFactory, _cache, _logger);
    }

    [Fact]
    public async Task GetPreferenceAsync_ReturnsFromCache_WhenPresent()
    {
        var cached = new UserThemePreference
        {
            UserId = "user-1",
            ThemeId = "cyberpunk",
            IsDarkMode = true
        };
        _cache.Set("user_theme_pref_user-1", cached);

        var result = await _service.GetPreferenceAsync("user-1");

        Assert.NotNull(result);
        Assert.Equal("cyberpunk", result.ThemeId);
        await _unitOfWork.UserThemePreferences.DidNotReceive().GetByUserIdAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetPreferenceAsync_QueriesRepositoryAndCaches_WhenCacheMiss()
    {
        var dbPref = new UserThemePreference
        {
            UserId = "user-2",
            ThemeId = "arctic",
            IsDarkMode = false
        };
        _unitOfWork.UserThemePreferences.GetByUserIdAsync("user-2").Returns(Task.FromResult<UserThemePreference?>(dbPref));

        var result = await _service.GetPreferenceAsync("user-2");

        Assert.NotNull(result);
        Assert.Equal("arctic", result.ThemeId);
        Assert.False(result.IsDarkMode);

        // Subsequent call should hit cache
        var cachedResult = await _service.GetPreferenceAsync("user-2");
        Assert.NotNull(cachedResult);
        await _unitOfWork.UserThemePreferences.Received(1).GetByUserIdAsync("user-2");
    }

    [Fact]
    public async Task SavePreferenceAsync_CallsRepositoryAndUpdatesCache()
    {
        await _service.SavePreferenceAsync("user-3", "midnight", true);

        await _unitOfWork.UserThemePreferences.Received(1).UpsertPreferenceAsync("user-3", "midnight", true);

        // Verify cache now has the updated value
        Assert.True(_cache.TryGetValue("user_theme_pref_user-3", out UserThemePreference? cached));
        Assert.NotNull(cached);
        Assert.Equal("midnight", cached.ThemeId);
        Assert.True(cached.IsDarkMode);
    }

    [Fact]
    public async Task GetThemePreferenceCountsAsync_ReturnsCountsFromRepository()
    {
        var expected = new List<ThemeUsageCount>
        {
            new("theme1", true, 5),
            new("theme2", false, 2)
        };
        _unitOfWork.UserThemePreferences.GetPreferenceCountsAsync().Returns(Task.FromResult(expected));

        var counts = await _service.GetThemePreferenceCountsAsync();

        Assert.Equal(2, counts.Count);
        Assert.Equal(5, counts[0].Count);
    }
}
