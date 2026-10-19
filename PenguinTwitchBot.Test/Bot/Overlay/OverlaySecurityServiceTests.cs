using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Overlay;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using System.Text.Json;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Overlay
{
    public class OverlaySecurityServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public OverlaySecurityServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection), ServiceLifetime.Scoped);
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Close();
            _connection.Dispose();
            GC.SuppressFinalize(this);
        }

        private OverlaySecurityService CreateSecurityService()
        {
            return new OverlaySecurityService(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<OverlaySecurityService>.Instance);
        }

        [Fact]
        public async Task GetOverlayKeyAsync_GeneratesAndPersistsKey_WhenNoSettingExists()
        {
            var service = CreateSecurityService();

            var key = await service.GetOverlayKeyAsync();

            Assert.False(string.IsNullOrWhiteSpace(key));
            Assert.Equal(32, key.Length);

            using var scope = _provider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var saved = (await unitOfWork.Settings.GetAsync(x => x.Name == OverlaySecurityService.SettingKey)).FirstOrDefault();

            Assert.NotNull(saved);
            Assert.Equal(key, saved.StringSetting);
        }

        [Fact]
        public async Task GetOverlayKeyAsync_ReturnsExistingPersistedKey()
        {
            const string existingKey = "test-existing-overlay-key-123456";

            using (var scope = _provider.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await unitOfWork.Settings.AddAsync(new Setting
                {
                    Name = OverlaySecurityService.SettingKey,
                    DataType = Setting.DataTypeEnum.String,
                    StringSetting = existingKey
                });
                await unitOfWork.SaveChangesAsync();
            }

            var service = CreateSecurityService();
            var resolvedKey = await service.GetOverlayKeyAsync();

            Assert.Equal(existingKey, resolvedKey);
        }


        [Fact]
        public async Task ValidateKeyAsync_ValidatesCorrectKey_AndRejectsInvalid()
        {
            var service = CreateSecurityService();
            var validKey = await service.GetOverlayKeyAsync();

            Assert.True(await service.ValidateKeyAsync(validKey));
            Assert.True(await service.ValidateKeyAsync($"  {validKey}  ")); // Trims whitespace
            Assert.False(await service.ValidateKeyAsync("wrong-key"));
            Assert.False(await service.ValidateKeyAsync(""));
            Assert.False(await service.ValidateKeyAsync(null));
        }

        [Fact]
        public async Task RegenerateOverlayKeyAsync_CreatesAndPersistsNewKey()
        {
            var service = CreateSecurityService();
            var initialKey = await service.GetOverlayKeyAsync();

            var newKey = await service.RegenerateOverlayKeyAsync();

            Assert.NotEqual(initialKey, newKey);
            Assert.Equal(newKey, await service.GetOverlayKeyAsync());

            using var scope = _provider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var saved = (await unitOfWork.Settings.GetAsync(x => x.Name == OverlaySecurityService.SettingKey)).FirstOrDefault();

            Assert.NotNull(saved);
            Assert.Equal(newKey, saved.StringSetting);
        }

    }
}

