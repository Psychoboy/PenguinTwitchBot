using DotNetTwitchBot.Bot.Commands.Fishing;
using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Fishing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotNetTwitchBot.Test.Bot.Commands.Fishing
{
    public class FishingLeaderboardServiceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FishingLeaderboardService _leaderboardService;
        private readonly ApplicationDbContext _context;
        private readonly string _databaseName;

        public FishingLeaderboardServiceTests()
        {
            _databaseName = $"FishingLeaderboardTestDb_{Guid.NewGuid()}";

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddLogging(builder => builder.AddConsole());

            _serviceProvider = services.BuildServiceProvider();
            _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();

            var logger = Substitute.For<ILogger<FishingLeaderboardService>>();
            _leaderboardService = new FishingLeaderboardService(_scopeFactory, logger);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _serviceProvider.Dispose();
        }

        private async Task SeedLeaderboardData()
        {
            var fishTypes = new List<FishType>
            {
                new() { Id = 1, Name = "Common Bass", Rarity = FishRarity.Common, BaseWeight = 10.0, BaseGold = 50, Enabled = true },
                new() { Id = 2, Name = "Rare Salmon", Rarity = FishRarity.Rare, BaseWeight = 20.0, BaseGold = 200, Enabled = true },
                new() { Id = 3, Name = "Legendary Marlin", Rarity = FishRarity.Legendary, BaseWeight = 100.0, BaseGold = 1000, Enabled = true }
            };
            _context.FishTypes.AddRange(fishTypes);

            var catches = new List<FishCatch>
            {
                // User 1: Multiple catches
                new() { Id = 1, UserId = "user1", Username = "Alice", FishTypeId = 1, FishType = fishTypes[0], 
                    Weight = 12.0, Stars = 3, GoldEarned = 75, CaughtAt = DateTime.UtcNow.AddHours(-5) },
                new() { Id = 2, UserId = "user1", Username = "Alice", FishTypeId = 2, FishType = fishTypes[1], 
                    Weight = 25.0, Stars = 4, GoldEarned = 300, CaughtAt = DateTime.UtcNow.AddHours(-3) },
                new() { Id = 3, UserId = "user1", Username = "Alice", FishTypeId = 1, FishType = fishTypes[0], 
                    Weight = 10.0, Stars = 2, GoldEarned = 50, CaughtAt = DateTime.UtcNow.AddHours(-1) },

                // User 2: High value catch
                new() { Id = 4, UserId = "user2", Username = "Bob", FishTypeId = 3, FishType = fishTypes[2], 
                    Weight = 120.0, Stars = 5, GoldEarned = 1500, CaughtAt = DateTime.UtcNow.AddHours(-2) },

                // User 3: Lower value catches
                new() { Id = 5, UserId = "user3", Username = "Charlie", FishTypeId = 1, FishType = fishTypes[0], 
                    Weight = 8.0, Stars = 1, GoldEarned = 40, CaughtAt = DateTime.UtcNow.AddHours(-4) },
                new() { Id = 6, UserId = "user3", Username = "Charlie", FishTypeId = 1, FishType = fishTypes[0], 
                    Weight = 9.0, Stars = 2, GoldEarned = 45, CaughtAt = DateTime.UtcNow.AddMinutes(-30) }
            };
            _context.FishCatches.AddRange(catches);
            await _context.SaveChangesAsync();
        }

        #region Total Gold Leaderboard Tests

        [Fact]
        public async Task GetTotalGoldLeaderboard_ReturnsOrderedByGold()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var leaderboard = await _leaderboardService.GetTotalGoldLeaderboard(10);

            // Assert
            Assert.NotNull(leaderboard);
            Assert.Equal(3, leaderboard.Count);

            // User2 (Bob) should be first with 1500 gold
            Assert.Equal(1, leaderboard[0].Rank);
            Assert.Equal("Bob", leaderboard[0].Name);
            Assert.Equal(1500, leaderboard[0].Amount);

            // User1 (Alice) should be second with 425 gold (75+300+50)
            Assert.Equal(2, leaderboard[1].Rank);
            Assert.Equal("Alice", leaderboard[1].Name);
            Assert.Equal(425, leaderboard[1].Amount);

            // User3 (Charlie) should be third with 85 gold (40+45)
            Assert.Equal(3, leaderboard[2].Rank);
            Assert.Equal("Charlie", leaderboard[2].Name);
            Assert.Equal(85, leaderboard[2].Amount);
        }

        [Fact]
        public async Task GetTotalGoldLeaderboard_LimitsResults()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var leaderboard = await _leaderboardService.GetTotalGoldLeaderboard(2);

            // Assert
            Assert.NotNull(leaderboard);
            Assert.Equal(2, leaderboard.Count);
            Assert.Equal("Bob", leaderboard[0].Name);
            Assert.Equal("Alice", leaderboard[1].Name);
        }

        [Fact]
        public async Task GetTotalGoldLeaderboard_EmptyDatabase_ReturnsEmpty()
        {
            // Act
            var leaderboard = await _leaderboardService.GetTotalGoldLeaderboard(10);

            // Assert
            Assert.NotNull(leaderboard);
            Assert.Empty(leaderboard);
        }

        [Fact]
        public async Task GetTotalGoldLeaderboard_ReturnsCorrectRanks()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var leaderboard = await _leaderboardService.GetTotalGoldLeaderboard(10);

            // Assert
            for (int i = 0; i < leaderboard.Count; i++)
            {
                Assert.Equal(i + 1, leaderboard[i].Rank);
            }
        }

        #endregion

        #region Most Valuable Catches Leaderboard Tests

        [Fact]
        public async Task GetMostValuableCatchesLeaderboard_ReturnsTopCatches()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var leaderboard = await _leaderboardService.GetMostValuableCatchesLeaderboard(10);

            // Assert
            Assert.NotNull(leaderboard);
            Assert.Equal(6, leaderboard.Count);

            // Legendary Marlin (1500 gold) should be first
            Assert.Equal(1500, leaderboard[0].GoldEarned);
            Assert.Equal("Legendary Marlin", leaderboard[0].FishType.Name);
            Assert.Equal("Bob", leaderboard[0].Username);

            // Rare Salmon (300 gold) should be second
            Assert.Equal(300, leaderboard[1].GoldEarned);
            Assert.Equal("Rare Salmon", leaderboard[1].FishType.Name);
            Assert.Equal("Alice", leaderboard[1].Username);
        }

        [Fact]
        public async Task GetMostValuableCatchesLeaderboard_LimitsResults()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var leaderboard = await _leaderboardService.GetMostValuableCatchesLeaderboard(3);

            // Assert
            Assert.NotNull(leaderboard);
            Assert.Equal(3, leaderboard.Count);
            Assert.Equal(1500, leaderboard[0].GoldEarned); // Legendary
            Assert.Equal(300, leaderboard[1].GoldEarned);  // Rare
            Assert.Equal(75, leaderboard[2].GoldEarned);   // First common
        }

        [Fact]
        public async Task GetMostValuableCatchesLeaderboard_IncludesFishTypeDetails()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var leaderboard = await _leaderboardService.GetMostValuableCatchesLeaderboard(1);

            // Assert
            var topCatch = leaderboard[0];
            Assert.NotNull(topCatch.FishType);
            Assert.Equal("Legendary Marlin", topCatch.FishType.Name);
            Assert.Equal(FishRarity.Legendary, topCatch.FishType.Rarity);
        }

        #endregion

        #region Recent Catches Tests

        [Fact]
        public async Task GetRecentCatches_ReturnsInChronologicalOrder()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var recent = await _leaderboardService.GetRecentCatches(10);

            // Assert
            Assert.NotNull(recent);
            Assert.Equal(6, recent.Count);
            
            // Most recent catch should be first (Charlie's second catch at -30 minutes)
            Assert.Equal("user3", recent[0].UserId);
            Assert.Equal(9.0, recent[0].Weight);

            // Oldest catch should be last (Alice's first catch at -5 hours)
            Assert.Equal("user1", recent[^1].UserId);
            Assert.Equal(12.0, recent[^1].Weight);
        }

        [Fact]
        public async Task GetRecentCatches_LimitsResults()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var recent = await _leaderboardService.GetRecentCatches(3);

            // Assert
            Assert.NotNull(recent);
            Assert.Equal(3, recent.Count);
        }

        [Fact]
        public async Task GetRecentCatches_IncludesFishTypeData()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var recent = await _leaderboardService.GetRecentCatches(5);

            // Assert
            Assert.All(recent, fishCatch => 
            {
                Assert.NotNull(fishCatch.FishType);
                Assert.NotNull(fishCatch.FishType.Name);
            });
        }

        #endregion

        #region User Recent Catches Tests

        [Fact]
        public async Task GetUserRecentCatches_ReturnsUserCatchesOnly()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var aliceCatches = await _leaderboardService.GetUserRecentCatches("user1", 10);

            // Assert
            Assert.NotNull(aliceCatches);
            Assert.Equal(3, aliceCatches.Count);
            Assert.All(aliceCatches, fishCatch => Assert.Equal("user1", fishCatch.UserId));
        }

        [Fact]
        public async Task GetUserRecentCatches_OrderedByTime()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var aliceCatches = await _leaderboardService.GetUserRecentCatches("user1", 10);

            // Assert
            // Most recent should be first (at -1 hour)
            Assert.Equal(10.0, aliceCatches[0].Weight);
            Assert.Equal(2, aliceCatches[0].Stars);

            // Oldest should be last (at -5 hours)
            Assert.Equal(12.0, aliceCatches[^1].Weight);
            Assert.Equal(3, aliceCatches[^1].Stars);
        }

        [Fact]
        public async Task GetUserRecentCatches_LimitsResults()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var aliceCatches = await _leaderboardService.GetUserRecentCatches("user1", 2);

            // Assert
            Assert.NotNull(aliceCatches);
            Assert.Equal(2, aliceCatches.Count);
        }

        [Fact]
        public async Task GetUserRecentCatches_NonExistentUser_ReturnsEmpty()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var catches = await _leaderboardService.GetUserRecentCatches("nonexistent-user", 10);

            // Assert
            Assert.NotNull(catches);
            Assert.Empty(catches);
        }

        [Fact]
        public async Task GetUserRecentCatches_IncludesFishTypeData()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var catches = await _leaderboardService.GetUserRecentCatches("user1", 10);

            // Assert
            Assert.All(catches, fishCatch =>
            {
                Assert.NotNull(fishCatch.FishType);
                Assert.NotNull(fishCatch.FishType.Name);
                Assert.True(fishCatch.FishType.Rarity >= FishRarity.Common);
            });
        }

        #endregion

        #region Performance and Edge Case Tests

        [Fact]
        public async Task GetTotalGoldLeaderboard_HandlesZeroLimit()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var leaderboard = await _leaderboardService.GetTotalGoldLeaderboard(0);

            // Assert
            Assert.NotNull(leaderboard);
            // Should return empty or handle gracefully
        }

        [Fact]
        public async Task GetMostValuableCatchesLeaderboard_HandlesNegativeLimit()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var leaderboard = await _leaderboardService.GetMostValuableCatchesLeaderboard(-1);

            // Assert
            Assert.NotNull(leaderboard);
            // Should handle gracefully (empty or error prevention)
        }

        [Fact]
        public async Task Leaderboards_HandleLargeLimits()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var goldLeaderboard = await _leaderboardService.GetTotalGoldLeaderboard(1000);
            var valuableLeaderboard = await _leaderboardService.GetMostValuableCatchesLeaderboard(1000);
            var recentCatches = await _leaderboardService.GetRecentCatches(1000);

            // Assert - Should not crash, returns all available data
            Assert.NotNull(goldLeaderboard);
            Assert.True(goldLeaderboard.Count <= 1000);
            Assert.NotNull(valuableLeaderboard);
            Assert.True(valuableLeaderboard.Count <= 1000);
            Assert.NotNull(recentCatches);
            Assert.True(recentCatches.Count <= 1000);
        }

        [Fact]
        public async Task Leaderboards_UseNoTracking()
        {
            // Arrange
            await SeedLeaderboardData();

            // Act
            var valuableLeaderboard = await _leaderboardService.GetMostValuableCatchesLeaderboard(10);

            // Modify returned data
            if (valuableLeaderboard.Any())
            {
                valuableLeaderboard[0].GoldEarned = 999999;
            }

            // Assert - Changes should not be tracked
            var freshValuableLeaderboard = await _leaderboardService.GetMostValuableCatchesLeaderboard(10);

            if (freshValuableLeaderboard.Any())
            {
                Assert.NotEqual(999999, freshValuableLeaderboard[0].GoldEarned);
            }
        }

        #endregion
    }
}
