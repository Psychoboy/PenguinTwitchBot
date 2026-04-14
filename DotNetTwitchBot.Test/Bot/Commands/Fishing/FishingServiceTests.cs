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
    public class FishingServiceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FishingService _fishingService;
        private readonly ApplicationDbContext _context;
        private readonly string _databaseName;

        public FishingServiceTests()
        {
            // Use a consistent database name so all contexts share the same in-memory database
            _databaseName = $"FishingTestDb_{Guid.NewGuid()}";

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddLogging(builder => builder.AddConsole());

            _serviceProvider = services.BuildServiceProvider();
            _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();

            var logger = Substitute.For<ILogger<FishingService>>();
            _fishingService = new FishingService(_scopeFactory, logger);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _serviceProvider.Dispose();
        }

        private async Task SeedTestData()
        {
            var fishTypes = new List<FishType>
            {
                new() { Id = 1, Name = "Common Bass", Rarity = FishRarity.Common, BaseWeight = 10.0, BaseGold = 50, Enabled = true },
                new() { Id = 2, Name = "Uncommon Trout", Rarity = FishRarity.Uncommon, BaseWeight = 15.0, BaseGold = 100, Enabled = true },
                new() { Id = 3, Name = "Rare Salmon", Rarity = FishRarity.Rare, BaseWeight = 20.0, BaseGold = 200, Enabled = true },
                new() { Id = 4, Name = "Epic Tuna", Rarity = FishRarity.Epic, BaseWeight = 50.0, BaseGold = 500, Enabled = true },
                new() { Id = 5, Name = "Legendary Marlin", Rarity = FishRarity.Legendary, BaseWeight = 100.0, BaseGold = 1000, Enabled = true },
                new() { Id = 6, Name = "Invalid Fish", Rarity = FishRarity.Common, BaseWeight = 0, BaseGold = 10, Enabled = true }
            };
            _context.FishTypes.AddRange(fishTypes);

            var settings = new FishingSettings
            { 
                Id = 1,
                Enabled = true,
                BoostMode = false,
                BoostModeRarityMultiplier = 2.0, // Default in FishingSettings.cs is 2.0
                DisplayDurationMs = 5000,
                RarityUncommonThreshold = 35,
                RarityRareThreshold = 60,
                RarityEpicThreshold = 110,
                RarityLegendaryThreshold = 201
            };
            _context.FishingSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        #region Diagnostic Tests

        [Fact]
        public async Task Diagnostic_VerifyShopItemIsStoredCorrectly()
        {
            await SeedTestData();

            var rod = new FishingShopItem
            {
                Id = 1,
                Name = "Test Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                Cost = 100,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.5,
                MaxUses = null,
                Enabled = true
            };
            _context.FishingShopItems.Add(rod);
            await _context.SaveChangesAsync();

            // Retrieve and verify
            var retrieved = await _context.FishingShopItems.FindAsync(1);
            Assert.NotNull(retrieved);
            Assert.Equal(FishingBoostType.GeneralRarityBoost, retrieved.BoostType);
            Assert.Equal(0.5, retrieved.BoostAmount);
        }

        #endregion

        #region Baseline Probability Tests

        [Fact]
        public async Task CalculateRarityProbabilities_NoBoosts_ReturnsBaselineProbabilities()
        {
            await SeedTestData();

            var result = await _fishingService.CalculateRarityProbabilities(false, 1.0, new List<int>());

            Assert.NotNull(result);
            // Base weights: Common=50, Uncommon=30, Rare=15, Epic=4, Legendary=1
            // Total = 100
            Assert.Equal(50.0, result.Probabilities[FishRarity.Common], 2);
            Assert.Equal(30.0, result.Probabilities[FishRarity.Uncommon], 2);
            Assert.Equal(15.0, result.Probabilities[FishRarity.Rare], 2);
            Assert.Equal(4.0, result.Probabilities[FishRarity.Epic], 2);
            Assert.Equal(1.0, result.Probabilities[FishRarity.Legendary], 2);
        }

        #endregion

        #region Single Boost Type Tests

        [Fact]
        public async Task CalculateRarityProbabilities_SingleGeneralRarityBoost_IncreasesNonCommonRarities()
        {
            await SeedTestData();

            var rod = new FishingShopItem
            {
                Id = 1,
                Name = "Basic Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                Cost = 100,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.5, // +50%
                MaxUses = null
            };
            _context.FishingShopItems.Add(rod);
            await _context.SaveChangesAsync();

            var result = await _fishingService.CalculateRarityProbabilities(false, 1.0, new List<int> { rod.Id });

            // With +50% boost: Uncommon=30*1.5=45, Rare=15*1.5=22.5, Epic=4*1.5=6, Legendary=1*1.5=1.5
            // Common stays 50
            // Total = 50 + 45 + 22.5 + 6 + 1.5 = 125
            // Percentages: Common=40%, Uncommon=36%, Rare=18%, Epic=4.8%, Legendary=1.2%
            Assert.True(result.Probabilities[FishRarity.Common] < 50.0, "Common should decrease as percentage");
            Assert.True(result.Probabilities[FishRarity.Uncommon] > 30.0, "Uncommon should increase");
            Assert.True(result.Probabilities[FishRarity.Rare] > 15.0, "Rare should increase");
            Assert.True(result.Probabilities[FishRarity.Legendary] > 1.0, "Legendary should increase");

            // Verify exact calculations
            Assert.Equal(40.0, result.Probabilities[FishRarity.Common], 1);
            Assert.Equal(36.0, result.Probabilities[FishRarity.Uncommon], 1);
            Assert.Equal(18.0, result.Probabilities[FishRarity.Rare], 1);
            Assert.Equal(4.8, result.Probabilities[FishRarity.Epic], 1);
            Assert.Equal(1.2, result.Probabilities[FishRarity.Legendary], 1);
        }

        #endregion

        #region Multi-Boost Equipment Tests (Tackle Box)

        [Fact]
        public async Task CalculateRarityProbabilities_TackleBoxWithDualBoosts_AppliesOnlyRarityBoost()
        {
            await SeedTestData();

            // Tackle box with GeneralRarityBoost + WeightBoost
            // WeightBoost should NOT affect rarity probabilities
            var tackleBox = new FishingShopItem
            {
                Id = 1,
                Name = "Advanced Tackle Box",
                EquipmentSlot = EquipmentSlot.TackleBox,
                Cost = 500,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.3, // +30%
                BoostType2 = FishingBoostType.WeightBoost,
                BoostAmount2 = 0.25, // +25% (should not affect rarity)
                MaxUses = null
            };
            _context.FishingShopItems.Add(tackleBox);
            await _context.SaveChangesAsync();

            var result = await _fishingService.CalculateRarityProbabilities(false, 1.0, new List<int> { tackleBox.Id });

            // With +30% boost: Uncommon=30*1.3=39, Rare=15*1.3=19.5, Epic=4*1.3=5.2, Legendary=1*1.3=1.3
            // Common stays 50
            // Total = 50 + 39 + 19.5 + 5.2 + 1.3 = 115
            // Percentages: Common=43.48%, Uncommon=33.91%, Rare=16.96%, Epic=4.52%, Legendary=1.13%
            Assert.Equal(43.48, result.Probabilities[FishRarity.Common], 1);
            Assert.Equal(33.91, result.Probabilities[FishRarity.Uncommon], 1);
            Assert.Equal(16.96, result.Probabilities[FishRarity.Rare], 1);
            Assert.Equal(4.52, result.Probabilities[FishRarity.Epic], 1);
            Assert.Equal(1.13, result.Probabilities[FishRarity.Legendary], 1);
        }

        [Fact]
        public async Task CalculateRarityProbabilities_TackleBoxWithTripleBoosts_AppliesBothRarityBoosts()
        {
            await SeedTestData();

            // Item with 3 boosts: 2 GeneralRarityBoosts + 1 WeightBoost
            var ultimateBox = new FishingShopItem
            {
                Id = 1,
                Name = "Ultimate Tackle Box",
                EquipmentSlot = EquipmentSlot.TackleBox,
                Cost = 1000,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.4, // +40%
                BoostType2 = FishingBoostType.WeightBoost,
                BoostAmount2 = 0.3, // +30% (should not affect rarity)
                BoostType3 = FishingBoostType.GeneralRarityBoost,
                BoostAmount3 = 0.2, // +20%
                MaxUses = null
            };
            _context.FishingShopItems.Add(ultimateBox);
            await _context.SaveChangesAsync();

            var result = await _fishingService.CalculateRarityProbabilities(false, 1.0, new List<int> { ultimateBox.Id });

            // Boosts stack multiplicatively: 1.4 * 1.2 = 1.68
            // Uncommon=30*1.68=50.4, Rare=15*1.68=25.2, Epic=4*1.68=6.72, Legendary=1*1.68=1.68
            // Common stays 50
            // Total = 50 + 50.4 + 25.2 + 6.72 + 1.68 = 134
            // Percentages: Common=37.31%, Uncommon=37.61%, Rare=18.81%, Epic=5.01%, Legendary=1.2537%
            Assert.Equal(37.31, result.Probabilities[FishRarity.Common], 1);
            Assert.Equal(37.61, result.Probabilities[FishRarity.Uncommon], 1);
            Assert.Equal(18.81, result.Probabilities[FishRarity.Rare], 1);
            Assert.Equal(5.01, result.Probabilities[FishRarity.Epic], 1);
            Assert.Equal(1.25, result.Probabilities[FishRarity.Legendary], 0);
        }

        #endregion

        #region Multiple Equipment Stacking Tests

        [Fact]
        public async Task CalculateRarityProbabilities_MultipleItems_BoostsStackMultiplicatively()
        {
            await SeedTestData();

            var rod = new FishingShopItem
            {
                Id = 1,
                Name = "Master Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                Cost = 300,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.3, // +30%
                MaxUses = null
            };

            var reel = new FishingShopItem
            {
                Id = 2,
                Name = "Precision Reel",
                EquipmentSlot = EquipmentSlot.Reel,
                Cost = 250,
                BoostType = FishingBoostType.StarBoost, // Should not affect rarity
                BoostAmount = 0.2,
                BoostType2 = FishingBoostType.GeneralRarityBoost,
                BoostAmount2 = 0.1, // +10%
                MaxUses = null
            };

            _context.FishingShopItems.AddRange(rod, reel);
            await _context.SaveChangesAsync();

            var result = await _fishingService.CalculateRarityProbabilities(false, 1.0, new List<int> { rod.Id, reel.Id });

            // Rod: +30% -> *1.3
            // Reel BoostType2: +10% -> *1.1
            // Combined: 1.3 * 1.1 = 1.43
            // Uncommon=30*1.43=42.9, Rare=15*1.43=21.45, Epic=4*1.43=5.72, Legendary=1*1.43=1.43
            // Common stays 50
            // Total = 50 + 42.9 + 21.45 + 5.72 + 1.43 = 121.5
            // Percentages: Common=41.15%, Uncommon=35.31%, Rare=17.654%, Epic=4.71%, Legendary=1.18%
            Assert.Equal(41.15, result.Probabilities[FishRarity.Common], 1);
            Assert.Equal(35.31, result.Probabilities[FishRarity.Uncommon], 1);
            Assert.Equal(17.65, result.Probabilities[FishRarity.Rare], 0);
            Assert.Equal(4.71, result.Probabilities[FishRarity.Epic], 1);
            Assert.Equal(1.18, result.Probabilities[FishRarity.Legendary], 1);
        }

        #endregion

        #region Global Boost Mode Tests

        [Fact]
        public async Task CalculateRarityProbabilities_GlobalBoostMode_MultipliesNonCommonRarities()
        {
            await SeedTestData();

            var result = await _fishingService.CalculateRarityProbabilities(true, 5.0, new List<int>());

            // With 5x global boost: Uncommon=30*5=150, Rare=15*5=75, Epic=4*5=20, Legendary=1*5=5
            // Common stays 50
            // Total = 50 + 150 + 75 + 20 + 5 = 300
            // Percentages: Common=16.67%, Uncommon=50%, Rare=25%, Epic=6.67%, Legendary=1.67%
            Assert.Equal(16.67, result.Probabilities[FishRarity.Common], 1);
            Assert.Equal(50.0, result.Probabilities[FishRarity.Uncommon], 1);
            Assert.Equal(25.0, result.Probabilities[FishRarity.Rare], 1);
            Assert.Equal(6.67, result.Probabilities[FishRarity.Epic], 1);
            Assert.Equal(1.67, result.Probabilities[FishRarity.Legendary], 1);
        }

        [Fact]
        public async Task CalculateRarityProbabilities_GlobalBoostWithEquipment_BothApply()
        {
            await SeedTestData();

            var rod = new FishingShopItem
            {
                Id = 1,
                Name = "Epic Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                Cost = 500,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.5, // +50%
                MaxUses = null
            };
            _context.FishingShopItems.Add(rod);
            await _context.SaveChangesAsync();

            var result = await _fishingService.CalculateRarityProbabilities(true, 3.0, new List<int> { rod.Id });

            // Global boost applies first: Uncommon=30*3=90, Rare=15*3=45, Epic=4*3=12, Legendary=1*3=3
            // Then equipment boost: Uncommon=90*1.5=135, Rare=45*1.5=67.5, Epic=12*1.5=18, Legendary=3*1.5=4.5
            // Common stays 50
            // Total = 50 + 135 + 67.5 + 18 + 4.5 = 275
            // Percentages: Common=18.18%, Uncommon=49.09%, Rare=24.545%, Epic=6.55%, Legendary=1.64%
            Assert.Equal(18.18, result.Probabilities[FishRarity.Common], 1);
            Assert.Equal(49.09, result.Probabilities[FishRarity.Uncommon], 1);
            Assert.Equal(24.55, result.Probabilities[FishRarity.Rare], 0);
            Assert.Equal(6.55, result.Probabilities[FishRarity.Epic], 0);
            Assert.Equal(1.64, result.Probabilities[FishRarity.Legendary], 1);
        }

        #endregion

        #region Catch Probabilities Consistency Tests

        [Fact]
        public async Task CalculateProbabilities_BoostModeConsistency_BetweenMethods()
        {
            await SeedTestData();

            var rod = new FishingShopItem
            {
                Id = 1,
                Name = "Test Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                Cost = 100,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.2,
                MaxUses = null
            };
            _context.FishingShopItems.Add(rod);
            await _context.SaveChangesAsync();

            var rarityProbs = await _fishingService.CalculateRarityProbabilities(true, 5.0, new List<int> { rod.Id });
            var fishProbs = await _fishingService.CalculateCatchProbabilities(true, 5.0, new List<int> { rod.Id });

            Assert.NotNull(rarityProbs);
            Assert.NotNull(fishProbs);
            Assert.NotEmpty(fishProbs);

            // For each rarity, sum of all fish OverallChance should equal the rarity tier probability
            foreach (var rarity in rarityProbs.Probabilities.Keys)
            {
                var fishInRarity = fishProbs.Values.Where(f => f.Rarity == rarity).ToList();
                if (fishInRarity.Any())
                {
                    var totalFishChance = fishInRarity.Sum(f => f.OverallChance);
                    Assert.Equal(rarityProbs.Probabilities[rarity], totalFishChance, 1);
                }
            }
        }

        [Fact]
        public async Task CalculateCatchProbabilities_AppliesAllBoostTypes()
        {
            await SeedTestData();

            var tackleBox = new FishingShopItem
            {
                Id = 1,
                Name = "Pro Tackle Box",
                EquipmentSlot = EquipmentSlot.TackleBox,
                Cost = 750,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.3,
                BoostType2 = FishingBoostType.WeightBoost,
                BoostAmount2 = 0.2,
                BoostType3 = FishingBoostType.GeneralRarityBoost,
                BoostAmount3 = 0.15,
                MaxUses = null
            };
            _context.FishingShopItems.Add(tackleBox);
            await _context.SaveChangesAsync();

            var result = await _fishingService.CalculateCatchProbabilities(false, 1.0, new List<int> { tackleBox.Id });

            Assert.NotNull(result);
            Assert.NotEmpty(result);

            // Verify legendary fish has higher chance than baseline
            var legendaryFish = result.Values.FirstOrDefault(f => f.Rarity == FishRarity.Legendary);
            Assert.NotNull(legendaryFish);
            // With both rarity boosts (1.3 * 1.15 = 1.495), legendary should be ~1.495% of total
            Assert.True(legendaryFish.RarityChance > 1.0, $"Legendary rarity chance was {legendaryFish.RarityChance}%");
        }

        #endregion

        #region Specific Fish Boost Tests

        [Fact]
        public async Task CalculateCatchProbabilities_SpecificFishBoost_IncreasesTargetFishTier()
        {
            await SeedTestData();

            var salmonBait = new FishingShopItem
            {
                Id = 1,
                Name = "Salmon Bait",
                EquipmentSlot = EquipmentSlot.Bait,
                Cost = 100,
                BoostType = FishingBoostType.SpecificFishBoost,
                BoostAmount = 1.0, // +100% to rare tier (where salmon is)
                TargetFishTypeId = 3, // Rare Salmon
                IsConsumable = true,
                MaxUses = 5
            };
            _context.FishingShopItems.Add(salmonBait);
            await _context.SaveChangesAsync();

            var result = await _fishingService.CalculateCatchProbabilities(false, 1.0, new List<int> { salmonBait.Id });

            var rareSalmon = result.Values.FirstOrDefault(f => f.FishName == "Rare Salmon");
            Assert.NotNull(rareSalmon);

            // Rare tier gets +100% boost: 15*2=30
            // Total = 50+30+30+4+1 = 115
            // Rare tier = 26.09%
            Assert.True(rareSalmon.RarityChance > 15.0, $"Rare tier chance was {rareSalmon.RarityChance}%");
        }

        [Fact]
        public async Task CalculateCatchProbabilities_MultipleSpecificBoostsSamefish_Stack()
        {
            await SeedTestData();

            var salmonBait = new FishingShopItem
            {
                Id = 1,
                Name = "Salmon Bait",
                EquipmentSlot = EquipmentSlot.Bait,
                Cost = 100,
                BoostType = FishingBoostType.SpecificFishBoost,
                BoostAmount = 0.5, // +50%
                TargetFishTypeId = 3,
                IsConsumable = true,
                MaxUses = 5
            };

            var salmonLure = new FishingShopItem
            {
                Id = 2,
                Name = "Salmon Lure",
                EquipmentSlot = EquipmentSlot.Lure,
                Cost = 150,
                BoostType = FishingBoostType.SpecificFishBoost,
                BoostAmount = 0.75, // +75%
                TargetFishTypeId = 3,
                IsConsumable = true,
                MaxUses = 3
            };

            _context.FishingShopItems.AddRange(salmonBait, salmonLure);
            await _context.SaveChangesAsync();

            var result = await _fishingService.CalculateCatchProbabilities(false, 1.0, new List<int> { salmonBait.Id, salmonLure.Id });

            var rareSalmon = result.Values.FirstOrDefault(f => f.FishName == "Rare Salmon");
            Assert.NotNull(rareSalmon);

            // Both boosts apply to rare tier: 1.5 * 1.75 = 2.625
            // Rare tier: 15 * 2.625 = 39.375
            // Total = 50 + 30 + 39.375 + 4 + 1 = 124.375
            // Rare tier = 31.66%
            Assert.Equal(31.66, rareSalmon.RarityChance, 1);
        }

        #endregion

        #region Edge Cases and Safety Tests

        [Fact]
        public async Task CalculateRarityProbabilities_EmptyItemList_ReturnsBaseline()
        {
            await SeedTestData();

            var result = await _fishingService.CalculateRarityProbabilities(false, 1.0, new List<int>());

            Assert.NotNull(result);
            Assert.Equal(50.0, result.Probabilities[FishRarity.Common], 2);
            Assert.Empty(result.ItemsEquipped);
        }

        [Fact]
        public async Task CalculateRarityProbabilities_NonExistentItemIds_IgnoresGracefully()
        {
            await SeedTestData();

            var result = await _fishingService.CalculateRarityProbabilities(false, 1.0, new List<int> { 999, 1000 });

            Assert.NotNull(result);
            Assert.Equal(50.0, result.Probabilities[FishRarity.Common], 2);
        }

        #endregion

        #region Settings Tests

        [Fact]
        public async Task GetSettings_ReturnsSettings()
        {
            await SeedTestData();

            var settings = await _fishingService.GetSettings();

            Assert.NotNull(settings);
            Assert.True(settings.Enabled);
            Assert.Equal(2.0, settings.BoostModeRarityMultiplier);
        }

        #endregion

        #region Complex Combination Tests

        [Fact]
        public async Task ComplexScenario_MaxLoadout_CalculatesCorrectly()
        {
            await SeedTestData();

            // Simulate a full loadout with multiple items
            var items = new List<FishingShopItem>
            {
                new() { Id = 1, Name = "Epic Rod", EquipmentSlot = EquipmentSlot.Rod, Cost = 500,
                    BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.35, MaxUses = null },
                new() { Id = 2, Name = "Pro Reel", EquipmentSlot = EquipmentSlot.Reel, Cost = 400,
                    BoostType = FishingBoostType.StarBoost, BoostAmount = 0.25,
                    BoostType2 = FishingBoostType.GeneralRarityBoost, BoostAmount2 = 0.15, MaxUses = null },
                new() { Id = 3, Name = "Ultimate Tackle Box", EquipmentSlot = EquipmentSlot.TackleBox, Cost = 1000,
                    BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.3,
                    BoostType2 = FishingBoostType.WeightBoost, BoostAmount2 = 0.25,
                    BoostType3 = FishingBoostType.GeneralRarityBoost, BoostAmount3 = 0.1, MaxUses = null }
            };
            _context.FishingShopItems.AddRange(items);
            await _context.SaveChangesAsync();

            var itemIds = items.Select(i => i.Id).ToList();
            var result = await _fishingService.CalculateRarityProbabilities(false, 1.0, itemIds);

            // Combined rarity multipliers: 1.35 * 1.15 * 1.3 * 1.1 = 2.22525
            // Uncommon=30*2.22525=66.76, Rare=15*2.22525=33.38, Epic=4*2.22525=8.90, Legendary=1*2.22525=2.23
            // Common=50
            // Total=161.27
            // Common=31.055%, Uncommon=41.40%, Rare=20.70%, Epic=5.52%, Legendary=1.38%
            Assert.Equal(31.06, result.Probabilities[FishRarity.Common], 1);
            Assert.Equal(41.40, result.Probabilities[FishRarity.Uncommon], 1);
            Assert.Equal(20.70, result.Probabilities[FishRarity.Rare], 1);
            Assert.Equal(5.52, result.Probabilities[FishRarity.Epic], 1);
            Assert.Equal(1.38, result.Probabilities[FishRarity.Legendary], 1);
        }

        [Fact]
        public async Task ComplexScenario_GlobalBoostPlusMaxLoadout_CalculatesCorrectly()
        {
            await SeedTestData();

            var rod = new FishingShopItem
            {
                Id = 1,
                Name = "Legendary Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                Cost = 1000,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.5,
                MaxUses = null
            };

            var tackleBox = new FishingShopItem
            {
                Id = 2,
                Name = "Legendary Tackle Box",
                EquipmentSlot = EquipmentSlot.TackleBox,
                Cost = 1500,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.4,
                BoostType2 = FishingBoostType.WeightBoost,
                BoostAmount2 = 0.35,
                BoostType3 = FishingBoostType.GeneralRarityBoost,
                BoostAmount3 = 0.25,
                MaxUses = null
            };

            _context.FishingShopItems.AddRange(rod, tackleBox);
            await _context.SaveChangesAsync();

            var result = await _fishingService.CalculateRarityProbabilities(true, 10.0, new List<int> { rod.Id, tackleBox.Id });

            // Global boost: 10x
            // Equipment: 1.5 * 1.4 * 1.25 = 2.625
            // Total multiplier: 10 * 2.625 = 26.25
            // Uncommon=30*26.25=787.5, Rare=15*26.25=393.75, Epic=4*26.25=105, Legendary=1*26.25=26.25
            // Common=50
            // Total=1362.5
            // Common=3.67%, Uncommon=57.80%, Rare=28.90%, Epic=7.71%, Legendary=1.93%
            Assert.Equal(3.67, result.Probabilities[FishRarity.Common], 1);
            Assert.Equal(57.80, result.Probabilities[FishRarity.Uncommon], 1);
            Assert.Equal(28.90, result.Probabilities[FishRarity.Rare], 1);
            Assert.Equal(7.71, result.Probabilities[FishRarity.Epic], 1);
            Assert.Equal(1.93, result.Probabilities[FishRarity.Legendary], 1);
        }

        #endregion

        #region Bug Fix Validation Tests

        [Fact]
        public async Task CalculateCatchProbabilities_DuplicateFishNames_UsesFishIdAsKey()
        {
            await SeedTestData();

            // Add two fish with the same name but different IDs to test the dictionary key collision fix
            var duplicateNameFish = new List<FishType>
            {
                new() { Id = 10, Name = "Duplicate Bass", Rarity = FishRarity.Common, BaseWeight = 8.0, BaseGold = 40, Enabled = true },
                new() { Id = 11, Name = "Duplicate Bass", Rarity = FishRarity.Uncommon, BaseWeight = 12.0, BaseGold = 80, Enabled = true }
            };
            _context.FishTypes.AddRange(duplicateNameFish);
            await _context.SaveChangesAsync();

            var result = await _fishingService.CalculateCatchProbabilities(new List<int>());

            // Verify that both fish are present in the result using fish ID as key (not name)
            Assert.True(result.ContainsKey(10), "Fish with ID 10 should be present in results");
            Assert.True(result.ContainsKey(11), "Fish with ID 11 should be present in results");

            // Verify that both fish maintain their distinct properties
            Assert.Equal("Duplicate Bass", result[10].FishName);
            Assert.Equal("Duplicate Bass", result[11].FishName);
            Assert.Equal(FishRarity.Common, result[10].Rarity);
            Assert.Equal(FishRarity.Uncommon, result[11].Rarity);

            // Verify no data loss - both fish should have different probabilities due to different rarities
            Assert.NotEqual(result[10].OverallChance, result[11].OverallChance);
        }

        [Fact]
        public async Task SelectRandomFish_SpecificFishBoostOnBoostType2_AppliesToCorrectFish()
        {
            await SeedTestData();

            // Create specific fish boost item with BoostType2
            var specificFishBoost = new FishingShopItem
            {
                Id = 1,
                Name = "Bass Hunter Lure",
                EquipmentSlot = EquipmentSlot.Hook,
                Cost = 200,
                BoostType = FishingBoostType.WeightBoost, // Primary boost: weight
                BoostAmount = 0.1,
                BoostType2 = FishingBoostType.SpecificFishBoost, // Secondary boost: specific fish
                BoostAmount2 = 2.0, // 200% boost to bass
                TargetFishTypeId = 1, // Common Bass (ID 1)
                MaxUses = null
            };
            _context.FishingShopItems.Add(specificFishBoost);
            await _context.SaveChangesAsync();

            // Simulate many fishing attempts to verify the boost is working
            var bassCount = 0;
            var totalAttempts = 1000;

            for (int i = 0; i < totalAttempts; i++)
            {
                var userBoost = new UserFishingBoost
                {
                    Id = i + 100,
                    UserId = "test-user",
                    ShopItemId = 1,
                    ShopItem = specificFishBoost,
                    IsEquipped = true,
                    RemainingUses = -1 // Unlimited uses
                };

                // Call the private method using reflection to test specific fish selection
                var method = typeof(FishingService).GetMethod("SelectRandomFish", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    var fishTypes = _context.FishTypes.Where(f => f.Enabled).ToList();
                    var boosts = new List<UserFishingBoost> { userBoost };
                    var settings = await _fishingService.GetSettings();

                    var selectedFish = (FishType)method.Invoke(_fishingService, new object[] { fishTypes, settings, boosts });
                    if (selectedFish.Id == 1) // Common Bass
                    {
                        bassCount++;
                    }
                }
            }

            // With a 200% boost to Common Bass (normally ~50% chance), we should see more bass
            // The boost should increase the rarity tier weight, making bass more likely but not dramatically
            var bassPercentage = (double)bassCount / totalAttempts * 100;

            // With the boost, bass should be caught more frequently than the baseline ~50%
            // A more conservative check for > 52% indicates the boost is working
            Assert.True(bassPercentage > 52.0, 
                $"SpecificFishBoost on BoostType2 should increase bass catch rate. Got {bassPercentage:F1}%");
        }

        [Fact]
        public async Task SelectRandomFish_SpecificFishBoostOnBoostType3_AppliesToCorrectFish()
        {
            await SeedTestData();

            // Create specific fish boost item with BoostType3
            var specificFishBoost = new FishingShopItem
            {
                Id = 1,
                Name = "Trout Master Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                Cost = 300,
                BoostType = FishingBoostType.WeightBoost, // Primary boost: weight
                BoostAmount = 0.1,
                BoostType2 = FishingBoostType.StarBoost, // Secondary boost: star quality
                BoostAmount2 = 0.15,
                BoostType3 = FishingBoostType.SpecificFishBoost, // Tertiary boost: specific fish
                BoostAmount3 = 1.5, // 150% boost to trout
                TargetFishTypeId = 2, // Uncommon Trout (ID 2)
                MaxUses = null
            };
            _context.FishingShopItems.Add(specificFishBoost);
            await _context.SaveChangesAsync();

            // Simulate many fishing attempts to verify the boost is working
            var troutCount = 0;
            var totalAttempts = 1000;

            for (int i = 0; i < totalAttempts; i++)
            {
                var userBoost = new UserFishingBoost
                {
                    Id = i + 100,
                    UserId = "test-user",
                    ShopItemId = 1,
                    ShopItem = specificFishBoost,
                    IsEquipped = true,
                    RemainingUses = -1 // Unlimited uses
                };

                // Call the private method using reflection to test specific fish selection
                var method = typeof(FishingService).GetMethod("SelectRandomFish", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    var fishTypes = _context.FishTypes.Where(f => f.Enabled).ToList();
                    var boosts = new List<UserFishingBoost> { userBoost };
                    var settings = await _fishingService.GetSettings();

                    var selectedFish = (FishType)method.Invoke(_fishingService, new object[] { fishTypes, settings, boosts });
                    if (selectedFish.Id == 2) // Uncommon Trout
                    {
                        troutCount++;
                    }
                }
            }

            // With a 150% boost to Uncommon Trout (normally ~30% chance), we should see more trout
            var troutPercentage = (double)troutCount / totalAttempts * 100;

            // With the boost, trout should be caught more frequently than the baseline ~30%
            // A conservative check for > 32% indicates the boost is working
            Assert.True(troutPercentage > 32.0, 
                $"SpecificFishBoost on BoostType3 should increase trout catch rate. Got {troutPercentage:F1}%");
        }

        [Fact]
        public async Task CalculateCatchProbabilities_ConsistentWithSelectRandomFish_BothUseFishId()
        {
            await SeedTestData();

            // Add a specific fish boost to test consistency
            var specificFishBoost = new FishingShopItem
            {
                Id = 1,
                Name = "Bass Targeting Lure",
                EquipmentSlot = null, // Consumable
                Cost = 100,
                BoostType = FishingBoostType.SpecificFishBoost,
                BoostAmount = 3.0, // 300% boost to bass
                TargetFishTypeId = 1, // Common Bass (ID 1)
                MaxUses = 5
            };
            _context.FishingShopItems.Add(specificFishBoost);
            await _context.SaveChangesAsync();

            // Get probability calculations
            var probabilities = await _fishingService.CalculateCatchProbabilities(new List<int> { 1 });

            // Verify the result uses fish ID as key and includes all enabled fish
            var enabledFishIds = _context.FishTypes.Where(f => f.Enabled).Select(f => f.Id).ToList();

            foreach (var fishId in enabledFishIds)
            {
                Assert.True(probabilities.ContainsKey(fishId), 
                    $"Probability calculation should include fish with ID {fishId}");
            }

            // Verify that Common Bass (ID 1) has a higher probability due to the specific boost
            var bassProb = probabilities[1];
            var otherFishProb = probabilities[2]; // Uncommon Trout

            // The specific boost should make bass significantly more likely
            Assert.True(bassProb.OverallChance > otherFishProb.OverallChance * 2,
                $"Bass with specific boost should have much higher probability. Bass: {bassProb.OverallChance:F2}%, Trout: {otherFishProb.OverallChance:F2}%");
        }

        #endregion
    }
}
