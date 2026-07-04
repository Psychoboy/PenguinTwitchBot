using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Test.Bot.Commands.Fishing
{
    public class FishingTournamentServiceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ApplicationDbContext _context;
        private readonly FishingService _fishingService;

        public FishingTournamentServiceTests()
        {
            var databaseName = $"FishingTournamentTestDb_{Guid.NewGuid()}";

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
            services.AddLogging();

            _serviceProvider = services.BuildServiceProvider();
            _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();

            _fishingService = new FishingService(
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                Substitute.For<ILogger<FishingService>>(),
                Substitute.For<IPointsSystem>());
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _serviceProvider.Dispose();
        }

        [Fact]
        public async Task StartFishingTournament_ActivatesAndBackfillsEndTime()
        {
            _context.FishingTournaments.Add(new FishingTournament
            {
                Id = 1,
                Name = "Test Tournament",
                Enabled = false,
                Status = FishingTournamentStatus.Scheduled,
                RunDurationMinutes = 30
            });
            await _context.SaveChangesAsync();

            var started = await _fishingService.StartFishingTournament(1);

            Assert.NotNull(started);
            Assert.Equal(FishingTournamentStatus.Active, started!.Status);
            Assert.True(started.Enabled);
            Assert.NotNull(started.StartsAtUtc);
            Assert.NotNull(started.EndsAtUtc);
            Assert.True(started.EndsAtUtc > started.StartsAtUtc);
        }
    }
}