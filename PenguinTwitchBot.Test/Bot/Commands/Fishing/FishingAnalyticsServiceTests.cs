using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Repository;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Fishing
{
    public class FishingAnalyticsServiceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ApplicationDbContext _context;
        private readonly SqliteConnection _connection;
        private readonly IFishingService _fishingService;
        private readonly FishingAnalyticsService _sut;

        public FishingAnalyticsServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddLogging(builder => builder.AddConsole());

            _serviceProvider = services.BuildServiceProvider();
            _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();
            _context.Database.EnsureCreated();

            _fishingService = Substitute.For<IFishingService>();
            var settings = new FishingSettings
            {
                Id = 1,
                LineSnapChance = 0.02,
                RodSnapChance = 0.001,
                ReelJamChance = 0.01,
                TackleBoxLostChance = 0.005,
                NetBreakChance = 0.01,
                RepairCostMultiplier = 0.25,
                BoostMode = false
            };
            _fishingService.GetSettings().Returns(Task.FromResult<FishingSettings?>(settings));

            var logger = Substitute.For<ILogger<FishingAnalyticsService>>();
            _sut = new FishingAnalyticsService(_scopeFactory, logger, _fishingService);

            SeedDatabase();
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
            _serviceProvider.Dispose();
        }

        private void SeedDatabase()
        {
            var fishTypes = new List<FishType>
            {
                new() { Id = 1, Name = "Minnow", Rarity = FishRarity.Common, BaseGold = 10, BaseWeight = 0.5, Enabled = true },
                new() { Id = 2, Name = "Bass", Rarity = FishRarity.Uncommon, BaseGold = 35, BaseWeight = 2.0, Enabled = true },
                new() { Id = 3, Name = "Salmon", Rarity = FishRarity.Rare, BaseGold = 75, BaseWeight = 5.0, Enabled = true },
                new() { Id = 4, Name = "Tuna", Rarity = FishRarity.Epic, BaseGold = 150, BaseWeight = 25.0, Enabled = true },
                new() { Id = 5, Name = "Shark", Rarity = FishRarity.Legendary, BaseGold = 300, BaseWeight = 100.0, Enabled = true }
            };
            _context.FishTypes.AddRange(fishTypes);

            var shopItems = new List<FishingShopItem>
            {
                new() { Id = 1, Name = "Bamboo Rod", Cost = 150, EquipmentSlot = EquipmentSlot.Rod, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.05, MaxDurability = 100, DurabilityLossPerUse = 1, Enabled = true },
                new() { Id = 2, Name = "Fiberglass Rod", Cost = 400, EquipmentSlot = EquipmentSlot.Rod, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.10, MaxDurability = 150, DurabilityLossPerUse = 1, Enabled = true },
                new() { Id = 3, Name = "Legendary Rod", Cost = 2500, EquipmentSlot = EquipmentSlot.Rod, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.25, MaxDurability = 300, DurabilityLossPerUse = 1, Enabled = true },
                new() { Id = 4, Name = "Basic Reel", Cost = 200, EquipmentSlot = EquipmentSlot.Reel, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.05, MaxDurability = 100, DurabilityLossPerUse = 1, Enabled = true },
                new() { Id = 5, Name = "Monofilament Line", Cost = 175, EquipmentSlot = EquipmentSlot.Line, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.10, MaxDurability = 50, DurabilityLossPerUse = 1, Enabled = true },
                new() { Id = 6, Name = "Standard Hook", Cost = 150, EquipmentSlot = EquipmentSlot.Hook, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.05, MaxDurability = 50, DurabilityLossPerUse = 1, Enabled = true },
                new() { Id = 7, Name = "Basic Tackle Box", Cost = 300, EquipmentSlot = EquipmentSlot.TackleBox, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.05, MaxDurability = 200, DurabilityLossPerUse = 1, Enabled = true },
                new() { Id = 8, Name = "Landing Net", Cost = 350, EquipmentSlot = EquipmentSlot.Net, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.15, MaxDurability = 150, DurabilityLossPerUse = 1, Enabled = true },
                new() { Id = 9, Name = "Worms", Cost = 75, EquipmentSlot = EquipmentSlot.Bait, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.15, IsConsumable = true, MaxUses = 5, Enabled = true },
                new() { Id = 10, Name = "Titanium Wire", Cost = 2800, EquipmentSlot = EquipmentSlot.Line, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.45, DisableBreaking = true, Enabled = true },
                new() { Id = 11, Name = "Carbon Fiber Rod", Cost = 1000, EquipmentSlot = EquipmentSlot.Rod, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.15, MaxDurability = 100, DurabilityLossPerUse = 2, DisableBreaking = true, Enabled = true }
            };
            _context.FishingShopItems.AddRange(shopItems);

            _context.FishingGolds.AddRange(
                new FishingGold { UserId = "u1", TotalGold = 500 },
                new FishingGold { UserId = "u2", TotalGold = 1200 },
                new FishingGold { UserId = "u3", TotalGold = 3500 }
            );

            _context.SaveChanges();
        }

        [Fact]
        public async Task CalculateBaselineExpectedGold_ReturnsPositiveTheoreticalGold()
        {
            var baselineGold = await _sut.CalculateBaselineExpectedGold();

            Assert.True(baselineGold > 0, "Baseline expected gold should be positive");
            Assert.True(baselineGold >= 10, "Baseline expected gold should be at least common fish base value");
        }

        [Fact]
        public async Task CalculateProjectedEconomy_ProducesAllProgressionTiersWithUpkeep()
        {
            var projected = await _sut.CalculateProjectedEconomy();

            Assert.NotNull(projected);
            Assert.True(projected.BaselineGrossGoldPerCatch > 0);
            Assert.True(projected.BaselineGrossGoldPerAttempt > 0);
            Assert.True(projected.BaselineSuccessRatePercent > 90);

            Assert.NotEmpty(projected.TierEconomics);
            var entryTier = projected.TierEconomics.FirstOrDefault(t => t.TierName == "Entry");
            Assert.NotNull(entryTier);
            Assert.True(entryTier.TotalLoadoutCost > 0);
            Assert.True(entryTier.DurabilityUpkeepPerAttempt > 0, "Entry tier should have durability upkeep");
            Assert.True(entryTier.AccidentSinkPerAttempt > 0, "Entry tier should have accident sink");
            Assert.True(entryTier.NetGoldPerAttempt > 0, "Entry tier should have positive net gold");
        }

        [Fact]
        public async Task SimulateScenario_CalculatesAccidentAndDurabilitySinks()
        {
            var scenario = new BalanceSimulationScenario
            {
                LineSnapChance = 0.01,
                RodSnapChance = 0.0005,
                ReelJamChance = 0.005,
                TackleBoxLostChance = 0.002,
                NetBreakChance = 0.005,
                RepairCostMultiplier = 0.25,
                AttemptsPerSession = 50,
                StreamsPerWeek = 4,
                SelectedLoadoutTier = "Entry"
            };

            var result = await _sut.SimulateScenario(scenario);

            Assert.NotNull(result);
            Assert.True(result.SuccessRatePercent > 95.0);
            Assert.True(result.GrossGoldPerAttempt > 0);
            Assert.True(result.DurabilityUpkeepPerAttempt > 0);
            Assert.True(result.AccidentSinkPerAttempt > 0);
            Assert.True(result.NetGoldPerSession > 0, "Balanced settings should yield positive net session gold");
            Assert.True(result.NetGoldPerWeek > 0);
            Assert.False(string.IsNullOrWhiteSpace(result.FinancialStatus));

            // Test harsh scenario detects deflation
            var harshScenario = new BalanceSimulationScenario
            {
                LineSnapChance = 0.10,
                RodSnapChance = 0.05,
                ReelJamChance = 0.05,
                TackleBoxLostChance = 0.05,
                NetBreakChance = 0.05,
                RepairCostMultiplier = 1.0,
                SelectedLoadoutTier = "Entry"
            };
            var harshResult = await _sut.SimulateScenario(harshScenario);
            Assert.True(harshResult.NetGoldPerAttempt < 0, "Harsh scenario should produce negative net gold");
            Assert.Contains("Deflationary", harshResult.FinancialStatus);
        }

        [Fact]
        public async Task CalculateItemEconomyAnalysis_CalculatesUpkeepAndPaybackForItems()
        {
            var analysis = await _sut.CalculateItemEconomyAnalysis();

            Assert.NotNull(analysis);
            Assert.NotEmpty(analysis);

            // Bamboo Rod with durability
            var bamboo = analysis.FirstOrDefault(i => i.ItemName == "Bamboo Rod");
            Assert.NotNull(bamboo);
            Assert.False(bamboo.IsUnbreakable);
            Assert.True(bamboo.DurabilityUpkeepPerAttempt > 0, "Bamboo Rod should incur durability upkeep");
            Assert.True(bamboo.AccidentRiskPerAttempt > 0, "Bamboo Rod should have accident risk");
            Assert.True(bamboo.ExpectedGrossGoldBoostPerAttempt >= 0, "Should calculate boost");

            // Titanium Wire with DisableBreaking and no durability
            var titanium = analysis.FirstOrDefault(i => i.ItemName == "Titanium Wire");
            Assert.NotNull(titanium);
            Assert.True(titanium.IsUnbreakable);
            Assert.Equal(0.0, titanium.DurabilityUpkeepPerAttempt);
            Assert.Equal(0.0, titanium.AccidentRiskPerAttempt);

            // Carbon Fiber Rod with DisableBreaking = true AND MaxDurability = 100
            var carbon = analysis.FirstOrDefault(i => i.ItemName == "Carbon Fiber Rod");
            Assert.NotNull(carbon);
            Assert.False(carbon.IsUnbreakable, "Item with MaxDurability should not be marked IsUnbreakable");
            Assert.True(carbon.DisableBreaking, "Item should have DisableBreaking set");
            Assert.True(carbon.DurabilityUpkeepPerAttempt > 0, "Item with DisableBreaking and MaxDurability should still incur durability upkeep");
            Assert.Equal(0.0, carbon.AccidentRiskPerAttempt); // immune to accident snap loss

            // Consumable Worms
            var worms = analysis.FirstOrDefault(i => i.ItemName == "Worms");
            Assert.NotNull(worms);
            Assert.True(worms.IsConsumable);
            Assert.True(worms.TotalOperatingCostPerAttempt > 0);
            Assert.Contains("Value", worms.EconomicRating);
        }

        [Fact]
        public async Task AnalyzeGameBalance_AggregatesRealAndProjectedTelemetry()
        {
            // Seed sample catches
            _context.FishCatches.AddRange(
                new FishCatch { UserId = "u1", FishTypeId = 1, GoldEarned = 12, CaughtAt = DateTime.UtcNow.AddDays(-2) },
                new FishCatch { UserId = "u1", FishTypeId = 2, GoldEarned = 38, CaughtAt = DateTime.UtcNow.AddDays(-2) },
                new FishCatch { UserId = "u2", FishTypeId = 3, GoldEarned = 80, CaughtAt = DateTime.UtcNow.AddDays(-1) }
            );

            // Seed sample snap/accident event
            _context.FishingSnapEvents.Add(new FishingSnapEvent
            {
                UserId = "u1",
                Username = "player1",
                SnapType = "Line",
                TotalGoldLost = 325,
                LostItemCount = 2,
                SnappedAt = DateTime.UtcNow.AddDays(-2)
            });

            // Seed equipped boost with durability for active fisher
            _context.UserFishingBoosts.Add(new UserFishingBoost
            {
                UserId = "u1",
                ShopItemId = 11, // Carbon Fiber Rod (1000g, 100 max dur, 2 loss, 0.25 repair multiplier -> 5g per attempt)
                IsEquipped = true,
                CurrentDurability = 100
            });

            await _context.SaveChangesAsync();

            var report = await _sut.AnalyzeGameBalance();

            Assert.NotNull(report);
            Assert.Equal(3, report.RealEconomy.TotalCatches);
            Assert.Equal(4, report.RealEconomy.TotalAttemptsRecorded); // 3 catches + 1 snap
            Assert.Equal(1, report.RealEconomy.TotalAccidentsRecorded);
            Assert.Equal(1, report.RealEconomy.LineSnapsRecorded);
            Assert.Equal(325m, report.RealEconomy.TotalGoldLostToAccidents);
            Assert.True(report.RealEconomy.AverageDurabilityUpkeepPerAttempt > 0, "Real durability upkeep should be calculated from active fishers equipped gear");
            Assert.True(report.RealEconomy.EstimatedDurabilityUpkeepIncurred > 0);

            Assert.NotNull(report.SettingsSnapshot);
            Assert.NotNull(report.ProjectedEconomy);
            Assert.NotEmpty(report.ItemAnalysis);
            Assert.NotEmpty(report.ProgressionMilestones);
            Assert.NotEmpty(report.Diagnostics);
        }

        [Fact]
        public async Task CalculateRecommendedPricing_ReturnsValidPricesForProgression()
        {
            var pricing = await _sut.CalculateRecommendedPricing(targetWeeksForEndgame: 26);

            Assert.NotNull(pricing);
            Assert.NotEmpty(pricing);
            Assert.True(pricing.ContainsKey("Bamboo Rod"));
            Assert.True(pricing.ContainsKey("Legendary Rod"));

            // Higher tier items should cost more than lower tier items
            Assert.True(pricing["Legendary Rod"] > pricing["Bamboo Rod"]);
        }
    }
}

