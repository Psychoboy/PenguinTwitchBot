using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Services
{
    public class TTSSettingsServiceTests
    {
        [Fact]
        public async Task GetKokoroThreadsAsync_ReturnsDefault_WhenSettingDoesNotExist()
        {
            // Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var settingsRepo = Substitute.For<ISettingsRepository>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(unitOfWork);
            unitOfWork.Settings.Returns(settingsRepo);

            settingsRepo.GetAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Setting, bool>>>())
                .ReturnsForAnyArgs(Task.FromResult(new List<Setting>()));

            var service = new TTSSettingsService(scopeFactory);

            // Act
            var result = await service.GetKokoroThreadsAsync(4);

            // Assert
            Assert.Equal(4, result);
            await settingsRepo.DidNotReceiveWithAnyArgs().AddAsync(default!);
            await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync();
        }

        [Fact]
        public async Task GetKokoroThreadsAsync_ReturnsExistingValue_WhenSettingExists()
        {
            // Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var settingsRepo = Substitute.For<ISettingsRepository>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(unitOfWork);
            unitOfWork.Settings.Returns(settingsRepo);

            var existingSetting = new Setting
            {
                Name = "KokoroThreads",
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = 3
            };

            settingsRepo.GetAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Setting, bool>>>())
                .ReturnsForAnyArgs(Task.FromResult(new List<Setting> { existingSetting }));

            var service = new TTSSettingsService(scopeFactory);

            // Act
            var result = await service.GetKokoroThreadsAsync(2);

            // Assert
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task SetKokoroThreadsAsync_ClampsBetween1AndProcessorCount_AndUpdatesSetting()
        {
            // Arrange
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var settingsRepo = Substitute.For<ISettingsRepository>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(unitOfWork);
            unitOfWork.Settings.Returns(settingsRepo);

            var existingSetting = new Setting
            {
                Name = "KokoroThreads",
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = 2
            };

            settingsRepo.GetAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Setting, bool>>>())
                .ReturnsForAnyArgs(Task.FromResult(new List<Setting> { existingSetting }));

            var service = new TTSSettingsService(scopeFactory);

            // Act - test value 0 clamps to 1
            await service.SetKokoroThreadsAsync(0);

            // Assert
            Assert.Equal(1, existingSetting.IntSetting);
            settingsRepo.Received(1).Update(existingSetting);
            await unitOfWork.Received(1).SaveChangesAsync();
        }
    }
}

