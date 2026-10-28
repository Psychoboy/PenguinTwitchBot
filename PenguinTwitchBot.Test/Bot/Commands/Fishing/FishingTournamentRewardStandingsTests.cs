using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Database.Repository;

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
            services.AddScoped<IUnitOfWork, UnitOfWork>();
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

        private Guid _pointTypeId;
        private Guid _fishTypeId;
        private Guid _tournamentId;

        private async Task SeedAsync(FishingTournamentStatus status, params FishingTournamentRewardRule[] rules)
        {
            _pointTypeId = Guid.CreateVersion7();
            _fishTypeId = Guid.CreateVersion7();
            _tournamentId = Guid.CreateVersion7();

            _context.PointTypes.Add(new PointType { Id = _pointTypeId, Name = "Coins" });
            _context.FishTypes.Add(new FishType { Id = _fishTypeId, Name = "Carp" });

            var tournament = new FishingTournament
            {
                Id = _tournamentId,
                Name = "Reward Test",
                Status = status,
                PrimaryScoreCategory = FishingTournamentScoreCategory.Largest,
                StartsAtUtc = DateTime.UtcNow.AddHours(-2),
                EndsAtUtc = status is FishingTournamentStatus.Completed ? DateTime.UtcNow.AddMinutes(-1) : null
            };

            foreach (var rule in rules)
            {
                rule.PointTypeId = _pointTypeId;
                tournament.RewardRules.Add(rule);
            }

            _context.FishingTournaments.Add(tournament);

            // Heaviest: bigfish (10kg). Most catches: smallfry (3 catches).
            var catches = new List<FishCatch>
            {
                new() { Id = Guid.CreateVersion7(), UserId = "u1", Username = "bigfish", FishTypeId = _fishTypeId, Weight = 10, GoldEarned = 5, CaughtAt = DateTime.UtcNow.AddMinutes(-30) },
                new() { Id = Guid.CreateVersion7(), UserId = "u2", Username = "smallfry", FishTypeId = _fishTypeId, Weight = 2, GoldEarned = 50, CaughtAt = DateTime.UtcNow.AddMinutes(-29) },
                new() { Id = Guid.CreateVersion7(), UserId = "u2", Username = "smallfry", FishTypeId = _fishTypeId, Weight = 3, GoldEarned = 10, CaughtAt = DateTime.UtcNow.AddMinutes(-28) },
                new() { Id = Guid.CreateVersion7(), UserId = "u2", Username = "smallfry", FishTypeId = _fishTypeId, Weight = 1, GoldEarned = 10, CaughtAt = DateTime.UtcNow.AddMinutes(-27) }
            };

            _context.FishCatches.AddRange(catches);
            foreach (var fishCatch in catches)
            {
                _context.FishingTournamentCatches.Add(new FishingTournamentCatch
                {
                    Id = Guid.CreateVersion7(),
                    FishingTournamentId = _tournamentId,
                    FishCatchId = fishCatch.Id,
                    UserId = fishCatch.UserId,
                    Username = fishCatch.Username,
                    FishTypeId = fishCatch.FishTypeId,
                    Stars = fishCatch.Stars,
                    Weight = fishCatch.Weight,
                    GoldEarned = fishCatch.GoldEarned,
                    CaughtAt = fishCatch.CaughtAt
                });
            }

            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task ReturnsLeaderPerRule_UsingEachRulesOwnScoreCategory()
        {
            var rule1 = new FishingTournamentRewardRule { Id = Guid.CreateVersion7(), Placement = 1, ScoreCategory = FishingTournamentScoreCategory.Largest, Points = 100 };
            var rule2 = new FishingTournamentRewardRule { Id = Guid.CreateVersion7(), Placement = 1, ScoreCategory = FishingTournamentScoreCategory.MostCatches, Points = 50 };
            await SeedAsync(FishingTournamentStatus.Active, rule1, rule2);

            var result = await _fishingService.GetFishingTournamentRewardStandings(_tournamentId);

            Assert.Equal("bigfish", result[rule1.Id].Username);
            Assert.Equal("smallfry", result[rule2.Id].Username);
        }

        [Fact]
        public async Task SecondPlaceRule_ReturnsRunnerUp()
        {
            var rule = new FishingTournamentRewardRule { Id = Guid.CreateVersion7(), Placement = 2, ScoreCategory = FishingTournamentScoreCategory.Largest, Points = 25 };
            await SeedAsync(FishingTournamentStatus.Active, rule);

            var result = await _fishingService.GetFishingTournamentRewardStandings(_tournamentId);

            Assert.Equal("smallfry", result[rule.Id].Username);
        }

        [Fact]
        public async Task DisabledRules_AreExcluded()
        {
            var rule = new FishingTournamentRewardRule { Id = Guid.CreateVersion7(), Placement = 1, ScoreCategory = FishingTournamentScoreCategory.Largest, Points = 100, Enabled = false };
            await SeedAsync(FishingTournamentStatus.Active, rule);

            var result = await _fishingService.GetFishingTournamentRewardStandings(_tournamentId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task CompletedTournament_StillResolvesWinners()
        {
            var rule = new FishingTournamentRewardRule { Id = Guid.CreateVersion7(), Placement = 1, ScoreCategory = FishingTournamentScoreCategory.Largest, Points = 100 };
            await SeedAsync(FishingTournamentStatus.Completed, rule);

            var result = await _fishingService.GetFishingTournamentRewardStandings(_tournamentId);

            Assert.Equal("bigfish", result[rule.Id].Username);
        }

        [Fact]
        public async Task PlacementDeeperThanEntrantCount_FallsBackToLastEntrant()
        {
            var rule = new FishingTournamentRewardRule { Id = Guid.CreateVersion7(), Placement = 5, ScoreCategory = FishingTournamentScoreCategory.Largest, Points = 10 };
            await SeedAsync(FishingTournamentStatus.Active, rule);

            var result = await _fishingService.GetFishingTournamentRewardStandings(_tournamentId);

            // Matches settlement's Take(Placement).LastOrDefault(): with fewer entrants than the
            // placement, the lowest-ranked entrant holds the reward.
            Assert.Equal("smallfry", result[rule.Id].Username);
        }

        [Fact]
        public async Task NoCatches_ReturnsEmpty()
        {
            var pointTypeId = Guid.CreateVersion7();
            var tournamentId = Guid.CreateVersion7();
            var ruleId = Guid.CreateVersion7();
            _context.PointTypes.Add(new PointType { Id = pointTypeId, Name = "Coins" });
            _context.FishingTournaments.Add(new FishingTournament
            {
                Id = tournamentId,
                Name = "Empty",
                Status = FishingTournamentStatus.Active,
                StartsAtUtc = DateTime.UtcNow.AddHours(-1),
                RewardRules = { new FishingTournamentRewardRule { Id = ruleId, Placement = 1, PointTypeId = pointTypeId, Points = 10 } }
            });
            await _context.SaveChangesAsync();

            var result = await _fishingService.GetFishingTournamentRewardStandings(tournamentId);

            Assert.Empty(result);
        }
    }
}
