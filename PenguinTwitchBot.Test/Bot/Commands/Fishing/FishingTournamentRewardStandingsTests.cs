using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Test.Bot.Commands.Fishing
{
    public class FishingTournamentRewardStandingsTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ApplicationDbContext _context;
        private readonly FishingService _fishingService;

        public FishingTournamentRewardStandingsTests()
        {
            var databaseName = $"RewardStandings_{Guid.NewGuid()}";

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName));
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
            GC.SuppressFinalize(this);
        }

        private async Task SeedAsync(FishingTournamentStatus status, params FishingTournamentRewardRule[] rules)
        {
            _context.PointTypes.Add(new PointType { Id = 1, Name = "Coins" });
            _context.FishTypes.Add(new FishType { Id = 1, Name = "Carp" });

            var tournament = new FishingTournament
            {
                Id = 1,
                Name = "Reward Test",
                Status = status,
                PrimaryScoreCategory = FishingTournamentScoreCategory.Largest,
                StartsAtUtc = DateTime.UtcNow.AddHours(-2),
                EndsAtUtc = status is FishingTournamentStatus.Completed ? DateTime.UtcNow.AddMinutes(-1) : null
            };

            foreach (var rule in rules)
            {
                tournament.RewardRules.Add(rule);
            }

            _context.FishingTournaments.Add(tournament);

            // Heaviest: bigfish (10kg). Most catches: smallfry (3 catches).
            var catches = new List<FishCatch>
            {
                new() { Id = 1, UserId = "u1", Username = "bigfish", FishTypeId = 1, Weight = 10, GoldEarned = 5, CaughtAt = DateTime.UtcNow.AddMinutes(-30) },
                new() { Id = 2, UserId = "u2", Username = "smallfry", FishTypeId = 1, Weight = 2, GoldEarned = 50, CaughtAt = DateTime.UtcNow.AddMinutes(-29) },
                new() { Id = 3, UserId = "u2", Username = "smallfry", FishTypeId = 1, Weight = 3, GoldEarned = 10, CaughtAt = DateTime.UtcNow.AddMinutes(-28) },
                new() { Id = 4, UserId = "u2", Username = "smallfry", FishTypeId = 1, Weight = 1, GoldEarned = 10, CaughtAt = DateTime.UtcNow.AddMinutes(-27) }
            };

            _context.FishCatches.AddRange(catches);
            foreach (var fishCatch in catches)
            {
                _context.FishingTournamentCatches.Add(new FishingTournamentCatch { FishingTournamentId = 1, FishCatchId = fishCatch.Id });
            }

            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task ReturnsLeaderPerRule_UsingEachRulesOwnScoreCategory()
        {
            await SeedAsync(
                FishingTournamentStatus.Active,
                new FishingTournamentRewardRule { Id = 10, Placement = 1, ScoreCategory = FishingTournamentScoreCategory.Largest, PointTypeId = 1, Points = 100 },
                new FishingTournamentRewardRule { Id = 11, Placement = 1, ScoreCategory = FishingTournamentScoreCategory.MostCatches, PointTypeId = 1, Points = 50 });

            var result = await _fishingService.GetFishingTournamentRewardStandings(1);

            Assert.Equal("bigfish", result[10].Username);
            Assert.Equal("smallfry", result[11].Username);
        }

        [Fact]
        public async Task SecondPlaceRule_ReturnsRunnerUp()
        {
            await SeedAsync(
                FishingTournamentStatus.Active,
                new FishingTournamentRewardRule { Id = 20, Placement = 2, ScoreCategory = FishingTournamentScoreCategory.Largest, PointTypeId = 1, Points = 25 });

            var result = await _fishingService.GetFishingTournamentRewardStandings(1);

            Assert.Equal("smallfry", result[20].Username);
        }

        [Fact]
        public async Task DisabledRules_AreExcluded()
        {
            await SeedAsync(
                FishingTournamentStatus.Active,
                new FishingTournamentRewardRule { Id = 30, Placement = 1, ScoreCategory = FishingTournamentScoreCategory.Largest, PointTypeId = 1, Points = 100, Enabled = false });

            var result = await _fishingService.GetFishingTournamentRewardStandings(1);

            Assert.Empty(result);
        }

        [Fact]
        public async Task CompletedTournament_StillResolvesWinners()
        {
            await SeedAsync(
                FishingTournamentStatus.Completed,
                new FishingTournamentRewardRule { Id = 40, Placement = 1, ScoreCategory = FishingTournamentScoreCategory.Largest, PointTypeId = 1, Points = 100 });

            var result = await _fishingService.GetFishingTournamentRewardStandings(1);

            Assert.Equal("bigfish", result[40].Username);
        }

        [Fact]
        public async Task PlacementDeeperThanEntrantCount_FallsBackToLastEntrant()
        {
            await SeedAsync(
                FishingTournamentStatus.Active,
                new FishingTournamentRewardRule { Id = 50, Placement = 5, ScoreCategory = FishingTournamentScoreCategory.Largest, PointTypeId = 1, Points = 10 });

            var result = await _fishingService.GetFishingTournamentRewardStandings(1);

            // Matches settlement's Take(Placement).LastOrDefault(): with fewer entrants than the
            // placement, the lowest-ranked entrant holds the reward.
            Assert.Equal("smallfry", result[50].Username);
        }

        [Fact]
        public async Task NoCatches_ReturnsEmpty()
        {
            _context.PointTypes.Add(new PointType { Id = 1, Name = "Coins" });
            _context.FishingTournaments.Add(new FishingTournament
            {
                Id = 1,
                Name = "Empty",
                Status = FishingTournamentStatus.Active,
                StartsAtUtc = DateTime.UtcNow.AddHours(-1),
                RewardRules = { new FishingTournamentRewardRule { Id = 60, Placement = 1, PointTypeId = 1, Points = 10 } }
            });
            await _context.SaveChangesAsync();

            var result = await _fishingService.GetFishingTournamentRewardStandings(1);

            Assert.Empty(result);
        }
    }
}
