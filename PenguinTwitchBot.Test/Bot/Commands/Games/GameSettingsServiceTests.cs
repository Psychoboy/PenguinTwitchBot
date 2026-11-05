using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Database.Bot.Models.Games;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Bot.Commands.Games
{
    public class GameSettingsServiceTests
    {
        [Fact]
        public async Task SetBoolSetting_ShouldBeReadable_AfterInitialCacheMiss()
        {
            // Arrange
            var logger = Substitute.For<ILogger<GameSettingsService>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var gameSettingsRepository = Substitute.For<IGameSettingsRepository>();
            var cache = new MemoryCache(new MemoryCacheOptions());

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(unitOfWork);
            unitOfWork.GameSettings.Returns(gameSettingsRepository);

            // Simulate DB miss for this setting so the first read caches a miss.
            gameSettingsRepository
                .GetAsync(Arg.Any<System.Linq.Expressions.Expression<Func<GameSetting, bool>>>(),
                    Arg.Any<Func<IQueryable<GameSetting>, IOrderedQueryable<GameSetting>>>(),
                    Arg.Any<int?>(),
                    Arg.Any<int?>(),
                    Arg.Any<string>())
                .ReturnsForAnyArgs(Task.FromResult(new List<GameSetting>()));

            var service = new GameSettingsService(logger, scopeFactory, cache);

            // Act
            var initial = await service.GetBoolSetting("GiveawayFeature", "GiveawayMonteCarloFairnessEnabled", false);
            await service.SetBoolSetting("GiveawayFeature", "GiveawayMonteCarloFairnessEnabled", true);
            var afterSet = await service.GetBoolSetting("GiveawayFeature", "GiveawayMonteCarloFairnessEnabled", false);

            // Assert
            Assert.False(initial);
            Assert.True(afterSet);
            gameSettingsRepository.Received(1).Update(Arg.Is<GameSetting>(x =>
                x.GameName == "giveawayfeature" &&
                x.SettingName == "giveawaymontecarlofairnessenabled" &&
                x.SettingBoolValue));
            await unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GetDefaultPointType_UsesDeterministicNameThenIdOrdering()
        {
            var logger = Substitute.For<ILogger<GameSettingsService>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var pointTypesRepository = Substitute.For<IPointTypesRepository>();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var zulu = new PointType { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Name = "Zulu" };
            var alphaHighId = new PointType { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "alpha" };
            var alphaLowId = new PointType { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "Alpha" };

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(unitOfWork);
            unitOfWork.PointTypes.Returns(pointTypesRepository);
            pointTypesRepository.GetAllAsync()
                .Returns(Task.FromResult<IEnumerable<PointType>>([zulu, alphaHighId, alphaLowId]));

            var service = new GameSettingsService(logger, scopeFactory, cache);

            var result = await service.GetDefaultPointType();

            Assert.Same(alphaLowId, result);
        }
    }
}
