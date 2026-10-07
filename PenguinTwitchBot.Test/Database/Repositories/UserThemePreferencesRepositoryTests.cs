using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Repository.Repositories;
using Xunit;

namespace PenguinTwitchBot.Test.Database.Repositories;

public class UserThemePreferencesRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly UserThemePreferencesRepository _repository;

    public UserThemePreferencesRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new UserThemePreferencesRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _repository.GetByUserIdAsync("non-existent-user");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertPreferenceAsync_InsertsNew_WhenNoneExists()
    {
        await _repository.UpsertPreferenceAsync("user-1", "cyberpunk", false);

        var saved = await _repository.GetByUserIdAsync("user-1");
        Assert.NotNull(saved);
        Assert.Equal("user-1", saved.UserId);
        Assert.Equal("cyberpunk", saved.ThemeId);
        Assert.False(saved.IsDarkMode);
    }

    [Fact]
    public async Task UpsertPreferenceAsync_UpdatesExisting_WhenRecordExists()
    {
        await _repository.UpsertPreferenceAsync("user-1", "cyberpunk", false);
        await _repository.UpsertPreferenceAsync("user-1", "arctic", true);

        var saved = await _repository.GetByUserIdAsync("user-1");
        Assert.NotNull(saved);
        Assert.Equal("arctic", saved.ThemeId);
        Assert.True(saved.IsDarkMode);

        var all = await _context.UserThemePreferences.ToListAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task GetPreferenceCountsAsync_ReturnsAggregatedCountsByThemeAndMode()
    {
        await _repository.UpsertPreferenceAsync("u1", "cyberpunk", true);
        await _repository.UpsertPreferenceAsync("u2", "cyberpunk", true);
        await _repository.UpsertPreferenceAsync("u3", "cyberpunk", false);
        await _repository.UpsertPreferenceAsync("u4", "default", true);

        var counts = await _repository.GetPreferenceCountsAsync();

        Assert.Equal(3, counts.Count);

        var cyberpunkDark = counts.FirstOrDefault(c => c.ThemeId == "cyberpunk" && c.IsDarkMode);
        Assert.NotNull(cyberpunkDark);
        Assert.Equal(2, cyberpunkDark.Count);

        var cyberpunkLight = counts.FirstOrDefault(c => c.ThemeId == "cyberpunk" && !c.IsDarkMode);
        Assert.NotNull(cyberpunkLight);
        Assert.Equal(1, cyberpunkLight.Count);

        var defaultDark = counts.FirstOrDefault(c => c.ThemeId == "default" && c.IsDarkMode);
        Assert.NotNull(defaultDark);
        Assert.Equal(1, defaultDark.Count);
    }
}

