using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Music;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Services
{
    public class SongCooldownServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;

        public SongCooldownServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            var services = new ServiceCollection();
            services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(new ApplicationDbContext(options)));
            var serviceProvider = services.BuildServiceProvider();

            _scopeFactory = Substitute.For<IServiceScopeFactory>();
            _scopeFactory.CreateScope().Returns(_ => serviceProvider.CreateScope());
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public async Task GetSettingsAsync_ReturnsDefaultSettings_WhenNotSet()
        {
            var service = new SongCooldownService(_scopeFactory, Substitute.For<IConfiguration>(), Substitute.For<ILogger<SongCooldownService>>());
            var settings = await service.GetSettingsAsync();

            Assert.False(settings.Enabled);
            Assert.Equal(60, settings.CooldownMinutes);
            Assert.False(settings.MessageEnabled);
            Assert.False(settings.ExemptSkippedVetoed);
            Assert.Contains("{0}", settings.Message);
        }

        [Fact]
        public async Task SaveSettingsAsync_PersistsSettings()
        {
            var service = new SongCooldownService(_scopeFactory, Substitute.For<IConfiguration>(), Substitute.For<ILogger<SongCooldownService>>());
            var newSettings = new SongCooldownSettings
            {
                Enabled = true,
                CooldownMinutes = 120,
                MessageEnabled = true,
                Message = "Custom msg {0} {1}",
                ExemptSkippedVetoed = true
            };

            await service.SaveSettingsAsync(newSettings);

            var loaded = await service.GetSettingsAsync();
            Assert.True(loaded.Enabled);
            Assert.Equal(120, loaded.CooldownMinutes);
            Assert.True(loaded.MessageEnabled);
            Assert.Equal("Custom msg {0} {1}", loaded.Message);
            Assert.True(loaded.ExemptSkippedVetoed);
        }

        [Fact]
        public async Task IsOnCooldownAsync_ReturnsFalse_WhenDisabled()
        {
            var service = new SongCooldownService(_scopeFactory, Substitute.For<IConfiguration>(), Substitute.For<ILogger<SongCooldownService>>());
            await service.AddCooldownAsync("dQw4w9WgXcQ", "Never Gonna Give You Up", TimeSpan.FromMinutes(60));

            var result = await service.IsOnCooldownAsync("dQw4w9WgXcQ");
            Assert.False(result); // Default enabled is false
        }

        [Fact]
        public async Task IsOnCooldownAsync_ReturnsTrue_WhenEnabledAndActive()
        {
            var service = new SongCooldownService(_scopeFactory, Substitute.For<IConfiguration>(), Substitute.For<ILogger<SongCooldownService>>());
            await service.SaveSettingsAsync(new SongCooldownSettings { Enabled = true, CooldownMinutes = 60 });

            await service.AddCooldownAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "Never Gonna Give You Up", TimeSpan.FromMinutes(60));

            var result = await service.IsOnCooldownAsync("dQw4w9WgXcQ");
            Assert.True(result);
        }

        [Fact]
        public async Task ClearCooldownAsync_RemovesCooldown()
        {
            var service = new SongCooldownService(_scopeFactory, Substitute.For<IConfiguration>(), Substitute.For<ILogger<SongCooldownService>>());
            await service.SaveSettingsAsync(new SongCooldownSettings { Enabled = true, CooldownMinutes = 60 });
            await service.AddCooldownAsync("dQw4w9WgXcQ", "Test Song", TimeSpan.FromMinutes(60));

            Assert.True(await service.IsOnCooldownAsync("dQw4w9WgXcQ"));

            await service.ClearCooldownAsync("dQw4w9WgXcQ");

            Assert.False(await service.IsOnCooldownAsync("dQw4w9WgXcQ"));
        }

        [Fact]
        public async Task ClearAllCooldownsAsync_RemovesAllCooldowns()
        {
            var service = new SongCooldownService(_scopeFactory, Substitute.For<IConfiguration>(), Substitute.For<ILogger<SongCooldownService>>());
            await service.SaveSettingsAsync(new SongCooldownSettings { Enabled = true, CooldownMinutes = 60 });

            await service.AddCooldownAsync("song1111111", "Song 1", TimeSpan.FromMinutes(60));
            await service.AddCooldownAsync("song2222222", "Song 2", TimeSpan.FromMinutes(60));

            var list = await service.GetActiveCooldownsAsync();
            Assert.Equal(2, list.Count);

            await service.ClearAllCooldownsAsync();

            var listAfter = await service.GetActiveCooldownsAsync();
            Assert.Empty(listAfter);
        }
    }
}
