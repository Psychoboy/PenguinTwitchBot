using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Fishing;
using DotNetTwitchBot.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public class FishingAnalyticsService : IFishingAnalyticsService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FishingAnalyticsService> _logger;
        private readonly IFishingService _fishingService;

        public FishingAnalyticsService(
            IServiceScopeFactory scopeFactory, 
            ILogger<FishingAnalyticsService> logger,
            IFishingService fishingService)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _fishingService = fishingService;
        }

        public async Task<FishingSimulationResult> SimulateFishing(int iterations, bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var result = new FishingSimulationResult
            {
                TotalIterations = iterations,
                BoostModeUsed = useBoostMode,
                BoostModeMultiplier = boostModeMultiplier
            };

            // Initialize counters
            foreach (FishRarity rarity in Enum.GetValues(typeof(FishRarity)))
            {
                result.RarityCounts[rarity] = 0;
            }
            result.StarCounts[1] = 0;
            result.StarCounts[2] = 0;
            result.StarCounts[3] = 0;

            // Get enabled fish types
            var fishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                throw new InvalidOperationException("No fish types available for simulation");
            }

            // Get shop items for simulation
            var shopItems = await context.FishingShopItems
                .Where(i => shopItemIds.Contains(i.Id))
                .ToListAsync();

            // Create mock user boosts from shop items
            var mockBoosts = shopItems.Select(item => new UserFishingBoost
            {
                UserId = "simulation",
                ShopItemId = item.Id,
                ShopItem = item,
                IsEquipped = true,
                RemainingUses = 999
            }).ToList();

            result.ItemsUsed = shopItems.Select(i => i.Name).ToList();

            // Get or create settings
            var settings = await _fishingService.GetSettings();
            if (settings == null)
            {
                settings = new FishingSettings();
            }

            // Override boost mode settings for simulation
            var simulationSettings = new FishingSettings
            {
                BoostMode = useBoostMode,
                BoostModeRarityMultiplier = boostModeMultiplier,
                RarityUncommonThreshold = settings.RarityUncommonThreshold,
                RarityRareThreshold = settings.RarityRareThreshold,
                RarityEpicThreshold = settings.RarityEpicThreshold,
                RarityLegendaryThreshold = settings.RarityLegendaryThreshold
            };

            var totalWeight = 0.0;
            var totalGold = 0;
            var minWeight = double.MaxValue;
            var maxWeight = 0.0;
            var heaviestFishName = string.Empty;

            // Run simulations
            for (int i = 0; i < iterations; i++)
            {
                var fish = FishingCalculations.SelectRandomFish(fishTypes, simulationSettings, mockBoosts);
                var stars = FishingCalculations.CalculateStars(fish, mockBoosts);
                var weight = FishingCalculations.CalculateWeight(fish, stars, mockBoosts);
                var gold = FishingCalculations.CalculateGold(fish, stars, weight);

                // Update counters
                result.RarityCounts[fish.Rarity]++;

                if (!result.FishCounts.ContainsKey(fish.Name))
                    result.FishCounts[fish.Name] = 0;
                result.FishCounts[fish.Name]++;

                result.StarCounts[stars]++;

                totalWeight += weight;
                totalGold += gold;

                if (weight < minWeight)
                    minWeight = weight;

                if (weight > maxWeight)
                {
                    maxWeight = weight;
                    heaviestFishName = fish.Name;
                }
            }

            // Calculate statistics
            result.AverageWeight = Math.Round(totalWeight / iterations, 2);
            result.AverageGold = Math.Round((double)totalGold / iterations, 2);
            result.TotalGold = totalGold;
            result.MinWeight = Math.Round(minWeight, 2);
            result.MaxWeight = Math.Round(maxWeight, 2);
            result.HeaviestFish = heaviestFishName;
            result.MostCommonFish = result.FishCounts.OrderByDescending(kvp => kvp.Value).First().Key;

            return result;
        }

        public async Task<Dictionary<int, FishProbability>> CalculateCatchProbabilities(List<int> shopItemIds)
        {
            var settings = await _fishingService.GetSettings();
            return await CalculateCatchProbabilities(settings?.BoostMode ?? false, settings?.BoostModeRarityMultiplier ?? 1.0, shopItemIds);
        }

        public async Task<Dictionary<int, FishProbability>> CalculateCatchProbabilities(bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get enabled fish types
            var fishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                return new Dictionary<int, FishProbability>();
            }

            // Get shop items
            var shopItems = await context.FishingShopItems
                .Include(s => s.TargetFishType)
                .Where(i => shopItemIds.Contains(i.Id))
                .ToListAsync();

            // Create mock boosts
            var mockBoosts = shopItems.Select(item => new UserFishingBoost
            {
                UserId = "calculation",
                ShopItemId = item.Id,
                ShopItem = item,
                IsEquipped = true,
                RemainingUses = 999
            }).ToList();

            // Calculate rarity weights (same logic as SelectRandomFish but for probability display)
            var rarityWeights = CalculateRarityWeights(fishTypes, useBoostMode, boostModeMultiplier, mockBoosts);
            var totalRarityWeight = rarityWeights.Values.Sum();

            // Calculate probabilities for each fish
            var probabilities = new Dictionary<int, FishProbability>();

            foreach (var fish in fishTypes)
            {
                var rarityChance = rarityWeights[fish.Rarity] / totalRarityWeight;
                var fishOfRarity = fishTypes.Where(f => f.Rarity == fish.Rarity).ToList();

                // Calculate specific fish weight within rarity
                var withinRarityChance = CalculateWithinRarityChance(fish, fishOfRarity, mockBoosts);
                var overallChance = rarityChance * withinRarityChance;

                probabilities[fish.Id] = new FishProbability
                {
                    FishId = fish.Id,
                    FishName = fish.Name,
                    Rarity = fish.Rarity,
                    RarityChance = Math.Round(rarityChance * 100, 4),
                    WithinRarityChance = Math.Round(withinRarityChance * 100, 4),
                    OverallChance = Math.Round(overallChance * 100, 4),
                    ExpectedAttemptsForOneCatch = overallChance > 0 ? (int)Math.Ceiling(1.0 / overallChance) : 0
                };
            }

            return probabilities;
        }

        public async Task<RarityProbability> CalculateRarityProbabilities(bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            var shopItems = await context.FishingShopItems
                .Include(s => s.TargetFishType)
                .Where(i => shopItemIds.Contains(i.Id))
                .ToListAsync();

            var mockBoosts = shopItems.Select(item => new UserFishingBoost
            {
                UserId = "calculation",
                ShopItemId = item.Id,
                ShopItem = item,
                IsEquipped = true,
                RemainingUses = 999
            }).ToList();

            var rarityWeights = CalculateRarityWeights(fishTypes, useBoostMode, boostModeMultiplier, mockBoosts);
            var totalWeight = rarityWeights.Values.Sum();

            var result = new RarityProbability
            {
                BoostModeActive = useBoostMode,
                BoostModeMultiplier = boostModeMultiplier,
                ItemsEquipped = shopItems.Select(i => i.Name).ToList(),
                Probabilities = new Dictionary<FishRarity, double>()
            };

            foreach (var kvp in rarityWeights)
            {
                result.Probabilities[kvp.Key] = Math.Round((kvp.Value / totalWeight) * 100, 4);
            }

            return result;
        }

        private Dictionary<FishRarity, double> CalculateRarityWeights(
            List<FishType> fishTypes,
            bool useBoostMode,
            double boostModeMultiplier,
            List<UserFishingBoost> mockBoosts)
        {
            var rarityWeights = new Dictionary<FishRarity, double>
            {
                { FishRarity.Common, 50.0 },
                { FishRarity.Uncommon, 30.0 },
                { FishRarity.Rare, 15.0 },
                { FishRarity.Epic, 4.0 },
                { FishRarity.Legendary, 1.0 }
            };

            if (useBoostMode)
            {
                rarityWeights[FishRarity.Uncommon] *= boostModeMultiplier;
                rarityWeights[FishRarity.Rare] *= boostModeMultiplier;
                rarityWeights[FishRarity.Epic] *= boostModeMultiplier;
                rarityWeights[FishRarity.Legendary] *= boostModeMultiplier;
            }

            foreach (var boost in mockBoosts)
            {
                ApplyBoostsToRarityWeights(rarityWeights, fishTypes, boost);
            }

            return rarityWeights;
        }

        private void ApplyBoostsToRarityWeights(
            Dictionary<FishRarity, double> rarityWeights,
            List<FishType> fishTypes,
            UserFishingBoost boost)
        {
            // Apply primary boost
            ApplySingleBoostToRarityWeights(rarityWeights, fishTypes, boost.ShopItem?.BoostType, boost.ShopItem?.BoostAmount ?? 0, boost.ShopItem?.TargetFishTypeId);
            // Apply secondary boost
            ApplySingleBoostToRarityWeights(rarityWeights, fishTypes, boost.ShopItem?.BoostType2, boost.ShopItem?.BoostAmount2 ?? 0, boost.ShopItem?.TargetFishTypeId);
            // Apply tertiary boost
            ApplySingleBoostToRarityWeights(rarityWeights, fishTypes, boost.ShopItem?.BoostType3, boost.ShopItem?.BoostAmount3 ?? 0, boost.ShopItem?.TargetFishTypeId);
        }

        private void ApplySingleBoostToRarityWeights(
            Dictionary<FishRarity, double> rarityWeights,
            List<FishType> fishTypes,
            FishingBoostType? boostType,
            double boostAmount,
            int? targetFishTypeId)
        {
            if (boostType == FishingBoostType.GeneralRarityBoost)
            {
                foreach (var rarity in rarityWeights.Keys.ToList())
                {
                    if (rarity != FishRarity.Common)
                    {
                        rarityWeights[rarity] *= (1.0 + boostAmount);
                    }
                }
            }
            else if (boostType == FishingBoostType.SpecificFishBoost && targetFishTypeId != null)
            {
                var targetFish = fishTypes.FirstOrDefault(f => f.Id == targetFishTypeId);
                if (targetFish != null)
                {
                    rarityWeights[targetFish.Rarity] *= (1.0 + boostAmount);
                }
            }
        }

        private double CalculateWithinRarityChance(FishType targetFish, List<FishType> fishOfRarity, List<UserFishingBoost> mockBoosts)
        {
            var specificBoosts = mockBoosts.Where(b => 
                (b.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost ||
                 b.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost ||
                 b.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost) &&
                b.ShopItem.TargetFishTypeId != null).ToList();

            if (specificBoosts.Any())
            {
                var weightedFish = new List<(FishType fish, double weight)>();
                foreach (var f in fishOfRarity)
                {
                    var weight = 1.0;
                    foreach (var boost in specificBoosts.Where(b => b.ShopItem?.TargetFishTypeId == f.Id))
                    {
                        if (boost.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost)
                            weight *= (1.0 + boost.ShopItem.BoostAmount);
                        if (boost.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost)
                            weight *= (1.0 + (boost.ShopItem.BoostAmount2 ?? 0));
                        if (boost.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost)
                            weight *= (1.0 + (boost.ShopItem.BoostAmount3 ?? 0));
                    }
                    weightedFish.Add((f, weight));
                }

                var totalFishWeight = weightedFish.Sum(w => w.weight);
                var fishWeight = weightedFish.First(w => w.fish.Id == targetFish.Id).weight;
                return fishWeight / totalFishWeight;
            }

            return 1.0 / fishOfRarity.Count;
        }

        public async Task<double> CalculateBaselineExpectedGold()
        {
            _logger.LogInformation("[BASELINE] CalculateBaselineExpectedGold() CALLED - Pure baseline (no equipment)");

            // Calculate expected gold per catch for a player with NO equipment
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                _logger.LogWarning("[BASELINE] No fish types found, returning 0");
                return 0.0;
            }

            var rarityWeights = new Dictionary<FishRarity, double>
            {
                { FishRarity.Common, 50.0 },
                { FishRarity.Uncommon, 30.0 },
                { FishRarity.Rare, 15.0 },
                { FishRarity.Epic, 4.0 },
                { FishRarity.Legendary, 1.0 }
            };

            var totalRarityWeight = rarityWeights.Values.Sum();
            var starProbabilities = new Dictionary<int, double>
            {
                { 1, 0.75 },  // 75% chance - NO BOOSTS
                { 2, 0.20 },  // 20% chance - NO BOOSTS
                { 3, 0.05 }   // 5% chance - NO BOOSTS
            };

            double expectedGold = 0.0;

            foreach (var (rarity, rarityWeight) in rarityWeights)
            {
                var fishOfRarity = fishTypes.Where(f => f.Rarity == rarity).ToList();
                if (!fishOfRarity.Any()) continue;

                var rarityProbability = rarityWeight / totalRarityWeight;
                var perFishProbability = rarityProbability / fishOfRarity.Count;

                foreach (var fish in fishOfRarity)
                {
                    foreach (var (stars, starProb) in starProbabilities)
                    {
                        var avgWeightMultiplier = (0.8 + 1.13) / 2.0;
                        var starWeightMultiplier = stars switch { 3 => 1.5, 2 => 1.2, _ => 1.0 };
                        var expectedWeight = fish.BaseWeight * avgWeightMultiplier * starWeightMultiplier;

                        var (minGoldMultiplier, maxGoldMultiplier) = stars switch
                        {
                            3 => (1.25, 1.41),
                            2 => (1.0, 1.25),
                            _ => (0.75, 1.0)
                        };

                        var avgGoldMultiplier = (minGoldMultiplier + maxGoldMultiplier) / 2.0;
                        var weightGoldMultiplier = 1.0;
                        if (fish.BaseWeight > 0)
                        {
                            var weightRatio = expectedWeight / fish.BaseWeight;
                            weightGoldMultiplier = 0.9 + ((weightRatio - 0.8) / (1.13 - 0.8) * 0.165);
                            weightGoldMultiplier = Math.Max(0.9, Math.Min(1.065, weightGoldMultiplier));
                        }

                        var gold = fish.BaseGold * avgGoldMultiplier * weightGoldMultiplier;
                        gold = Math.Max(1, gold);
                        expectedGold += perFishProbability * starProb * gold;
                    }
                }
            }

            var result = Math.Round(expectedGold, 2);
            _logger.LogInformation("[BASELINE] Pure baseline result: {Result}g/catch (NO equipment boosts)", result);
            return result;
        }

        public async Task<double> CalculateProgressiveBaselineGold(int targetWeeks = 26)
        {
            _logger.LogInformation("[PROGRESSIVE] CalculateProgressiveBaselineGold() CALLED - targetWeeks: {TargetWeeks}", targetWeeks);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                _logger.LogWarning("[PROGRESSIVE] No fish types found, returning 0");
                return 0.0;
            }

            // Define progression tiers
            var tiers = new List<ProgressionTier>
            {
                new ProgressionTier { Name = "Naked", Weeks = 2, RarityBoost = 0.0, StarBoost = 0.0, WeightBoost = 0.0 },
                new ProgressionTier { Name = "Entry", Weeks = 5, RarityBoost = 0.05, StarBoost = 0.10, WeightBoost = 0.10 },
                new ProgressionTier { Name = "Mid", Weeks = 6, RarityBoost = 0.10, StarBoost = 0.20, WeightBoost = 0.20 },
                new ProgressionTier { Name = "High", Weeks = 7, RarityBoost = 0.15, StarBoost = 0.30, WeightBoost = 0.30 },
                new ProgressionTier { Name = "Top", Weeks = 6, RarityBoost = 0.25, StarBoost = 0.42, WeightBoost = 0.45 }
            };

            if (targetWeeks != 26)
            {
                var scaleFactor = targetWeeks / 26.0;
                foreach (var tier in tiers)
                {
                    tier.Weeks = Math.Max(1, (int)Math.Round(tier.Weeks * scaleFactor));
                }
            }

            var totalWeeks = tiers.Sum(t => t.Weeks);
            double weightedGold = 0.0;

            _logger.LogInformation("[PROGRESSIVE] Tier breakdown:");
            foreach (var tier in tiers)
            {
                var tierGold = CalculateExpectedGoldWithBoosts(fishTypes, tier.RarityBoost, tier.StarBoost, tier.WeightBoost);
                var tierWeight = tier.Weeks / (double)totalWeeks;
                var contribution = tierGold * tierWeight;
                weightedGold += contribution;

                _logger.LogInformation("[PROGRESSIVE]   {Name}: {Weeks}wk, {Gold}g/catch × {Weight:P1} = {Contribution}g", 
                    tier.Name, tier.Weeks, Math.Round(tierGold, 2), tierWeight, Math.Round(contribution, 2));
            }

            var result = Math.Round(weightedGold, 2);
            _logger.LogInformation("[PROGRESSIVE] Progressive baseline result: {Result}g/catch (WITH equipment progression)", result);
            return result;
        }

        private double CalculateExpectedGoldWithBoosts(
            List<FishType> fishTypes,
            double rarityBoost,
            double starBoost,
            double weightBoost)
        {
            var rarityWeights = new Dictionary<FishRarity, double>
            {
                { FishRarity.Common, 50.0 },
                { FishRarity.Uncommon, 30.0 },
                { FishRarity.Rare, 15.0 },
                { FishRarity.Epic, 4.0 },
                { FishRarity.Legendary, 1.0 }
            };

            if (rarityBoost > 0)
            {
                rarityWeights[FishRarity.Uncommon] *= (1.0 + rarityBoost);
                rarityWeights[FishRarity.Rare] *= (1.0 + rarityBoost);
                rarityWeights[FishRarity.Epic] *= (1.0 + rarityBoost);
                rarityWeights[FishRarity.Legendary] *= (1.0 + rarityBoost);
            }

            var totalRarityWeight = rarityWeights.Values.Sum();

            var threeStarChance = 5.0 + (starBoost * 100);
            var twoStarChance = 20.0 + (starBoost * 100);
            var oneStarChance = 100.0 - threeStarChance - twoStarChance;

            var starProbabilities = new Dictionary<int, double>
            {
                { 1, oneStarChance / 100.0 },
                { 2, twoStarChance / 100.0 },
                { 3, threeStarChance / 100.0 }
            };

            var weightMultiplier = 1.0 + weightBoost;
            double expectedGold = 0.0;

            foreach (var (rarity, rarityWeight) in rarityWeights)
            {
                var fishOfRarity = fishTypes.Where(f => f.Rarity == rarity).ToList();
                if (!fishOfRarity.Any()) continue;

                var rarityProbability = rarityWeight / totalRarityWeight;
                var perFishProbability = rarityProbability / fishOfRarity.Count;

                foreach (var fish in fishOfRarity)
                {
                    foreach (var (stars, starProb) in starProbabilities)
                    {
                        var avgWeightMultiplier = (0.8 + 1.13) / 2.0;
                        var starWeightMultiplier = stars switch { 3 => 1.5, 2 => 1.2, _ => 1.0 };
                        var expectedWeight = fish.BaseWeight * avgWeightMultiplier * starWeightMultiplier * weightMultiplier;

                        var (minGoldMultiplier, maxGoldMultiplier) = stars switch
                        {
                            3 => (1.25, 1.41),
                            2 => (1.0, 1.25),
                            _ => (0.75, 1.0)
                        };

                        var avgGoldMultiplier = (minGoldMultiplier + maxGoldMultiplier) / 2.0;
                        var weightGoldMultiplier = 1.0;
                        if (fish.BaseWeight > 0)
                        {
                            var weightRatio = expectedWeight / fish.BaseWeight;
                            weightGoldMultiplier = 0.9 + ((weightRatio - 0.8) / (1.13 - 0.8) * 0.165);
                            weightGoldMultiplier = Math.Max(0.9, Math.Min(1.065, weightGoldMultiplier));
                        }

                        var gold = fish.BaseGold * avgGoldMultiplier * weightGoldMultiplier;
                        gold = Math.Max(1, gold);
                        expectedGold += perFishProbability * starProb * gold;
                    }
                }
            }

            return expectedGold;
        }

        private class ProgressionTier
        {
            public string Name { get; set; } = string.Empty;
            public int Weeks { get; set; }
            public double RarityBoost { get; set; }
            public double StarBoost { get; set; }
            public double WeightBoost { get; set; }
        }

        public async Task<FishingBalanceReport> AnalyzeGameBalance(DateTime? startDate = null, DateTime? endDate = null)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var report = new FishingBalanceReport
            {
                StartDate = startDate,
                EndDate = endDate
            };

            // Build query for catches within date range
            var catchesQuery = context.FishCatches
                .Include(c => c.FishType)
                .AsQueryable();

            if (startDate.HasValue)
                catchesQuery = catchesQuery.Where(c => c.CaughtAt >= startDate.Value);
            if (endDate.HasValue)
                catchesQuery = catchesQuery.Where(c => c.CaughtAt <= endDate.Value);

            var catches = await catchesQuery.ToListAsync();

            // Basic statistics
            report.TotalCatches = catches.Count;
            report.UniqueUsers = catches.Select(c => c.UserId).Distinct().Count();

            if (report.TotalCatches == 0)
            {
                report.Summary = "No catches found in the specified date range.";
                return report;
            }

            // Rarity distribution
            var rarityGroups = catches.GroupBy(c => c.FishType.Rarity)
                .Select(g => new { Rarity = g.Key, Count = g.Count() })
                .ToList();

            foreach (var group in rarityGroups)
            {
                report.RarityDistribution[group.Rarity] = group.Count;
                report.RarityPercentages[group.Rarity] = Math.Round((double)group.Count / report.TotalCatches * 100, 2);
            }

            // Ensure all rarities are represented
            foreach (FishRarity rarity in Enum.GetValues(typeof(FishRarity)))
            {
                if (!report.RarityDistribution.ContainsKey(rarity))
                {
                    report.RarityDistribution[rarity] = 0;
                    report.RarityPercentages[rarity] = 0;
                }
            }

            // Star distribution
            var starGroups = catches.GroupBy(c => c.Stars)
                .Select(g => new { Stars = g.Key, Count = g.Count() })
                .ToList();

            foreach (var group in starGroups)
            {
                report.StarDistribution[group.Stars] = group.Count;
                report.StarPercentages[group.Stars] = Math.Round((double)group.Count / report.TotalCatches * 100, 2);
            }

            // Gold economics
            var goldValues = catches.Select(c => c.GoldEarned).OrderBy(g => g).ToList();
            report.TotalGoldEarned = goldValues.Sum();
            report.AverageGoldPerCatch = Math.Round((double)report.TotalGoldEarned / report.TotalCatches, 2);
            report.MedianGoldPerCatch = goldValues.Count % 2 == 0
                ? (goldValues[goldValues.Count / 2 - 1] + goldValues[goldValues.Count / 2]) / 2.0
                : goldValues[goldValues.Count / 2];
            report.MinGoldEarned = goldValues.Min();
            report.MaxGoldEarned = goldValues.Max();

            // Per-user statistics
            var userGroups = catches.GroupBy(c => c.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Username = g.First().Username,
                    CatchCount = g.Count(),
                    TotalGold = g.Sum(c => c.GoldEarned)
                })
                .ToList();

            report.AverageCatchesPerUser = Math.Round((double)report.TotalCatches / report.UniqueUsers, 2);
            report.AverageGoldPerUser = Math.Round((double)report.TotalGoldEarned / report.UniqueUsers, 2);

            report.TopUsersByCatches = userGroups
                .OrderByDescending(u => u.CatchCount)
                .Take(10)
                .ToDictionary(u => u.Username, u => u.CatchCount);

            report.TopUsersByGold = userGroups
                .OrderByDescending(u => u.TotalGold)
                .Take(10)
                .ToDictionary(u => u.Username, u => u.TotalGold);

            // Fish distribution
            var fishGroups = catches.GroupBy(c => c.FishType.Name)
                .Select(g => new { FishName = g.Key, Count = g.Count() })
                .ToList();

            foreach (var group in fishGroups)
            {
                report.FishCatchCounts[group.FishName] = group.Count;
                report.FishCatchPercentages[group.FishName] = Math.Round((double)group.Count / report.TotalCatches * 100, 2);
            }

            if (fishGroups.Any())
            {
                report.MostCaughtFish = fishGroups.OrderByDescending(g => g.Count).First().FishName;
                report.LeastCaughtFish = fishGroups.OrderBy(g => g.Count).First().FishName;
            }

            // Get boost mode settings
            var settings = await _fishingService.GetSettings();
            report.BoostModeActive = settings?.BoostMode;
            report.BoostModeMultiplier = settings?.BoostModeRarityMultiplier;

            // Expected rarity percentages (theoretical baseline without boosts)
            var baselineRarities = new Dictionary<FishRarity, double>
            {
                { FishRarity.Common, 50.0 },
                { FishRarity.Uncommon, 30.0 },
                { FishRarity.Rare, 15.0 },
                { FishRarity.Epic, 4.0 },
                { FishRarity.Legendary, 1.0 }
            };

            var totalWeight = baselineRarities.Values.Sum();
            foreach (var kvp in baselineRarities)
            {
                report.ExpectedRarityPercentages[kvp.Key] = Math.Round((kvp.Value / totalWeight) * 100, 2);
                var actualPercent = report.RarityPercentages.ContainsKey(kvp.Key) ? report.RarityPercentages[kvp.Key] : 0;
                report.RarityVariance[kvp.Key] = Math.Round(actualPercent - report.ExpectedRarityPercentages[kvp.Key], 2);
            }

            // Item affordability analysis
            var shopItems = await context.FishingShopItems.Where(i => i.Enabled).ToListAsync();
            var userGoldTotals = userGroups.Select(u => u.TotalGold).OrderBy(g => g).ToList();
            var medianUserGold = userGoldTotals.Count > 0
                ? (userGoldTotals.Count % 2 == 0
                    ? (userGoldTotals[userGoldTotals.Count / 2 - 1] + userGoldTotals[userGoldTotals.Count / 2]) / 2.0
                    : userGoldTotals[userGoldTotals.Count / 2])
                : 0;

            foreach (var item in shopItems)
            {
                var affordability = new ItemAffordability
                {
                    ItemName = item.Name,
                    Cost = item.Cost,
                    IsConsumable = item.IsConsumable,
                    MaxUses = item.MaxUses,
                    EquipmentSlot = item.EquipmentSlot.ToString(),
                    CostPerUse = item.IsConsumable && item.MaxUses.HasValue && item.MaxUses > 0
                        ? Math.Round((double)item.Cost / item.MaxUses.Value, 2)
                        : item.Cost,
                    MedianUserGold = medianUserGold
                };

                // How many users can afford this item right now
                affordability.UsersWhoCanAfford = userGroups.Count(u => u.TotalGold >= item.Cost);
                affordability.PercentageWhoCanAfford = report.UniqueUsers > 0
                    ? Math.Round((double)affordability.UsersWhoCanAfford / report.UniqueUsers * 100, 2)
                    : 0;

                // How many catches needed to afford
                affordability.CatchesNeededToBuy = report.AverageGoldPerCatch > 0
                    ? Math.Round(item.Cost / report.AverageGoldPerCatch, 1)
                    : 0;

                // Days to afford (assuming 3 catches per week = ~0.43 per day)
                affordability.DaysToAfford3xWeek = affordability.CatchesNeededToBuy > 0
                    ? Math.Round(affordability.CatchesNeededToBuy / 0.43, 1)
                    : 0;

                // Days to afford (assuming 1 catch per day)
                affordability.DaysToAfford1xDay = affordability.CatchesNeededToBuy;

                // Affordability rating for permanent items
                // Aligned with progressive baseline model (5 tiers over 26 weeks)
                // Low=1-3wk, Mid=3-8wk, High=8-16wk, Top=16+ weeks
                if (!item.IsConsumable)
                {
                    var weeksToAfford = affordability.DaysToAfford3xWeek / 7.0;

                    if (weeksToAfford < 1)
                        affordability.AffordabilityRating = "Too Cheap";
                    else if (weeksToAfford < 3)
                        affordability.AffordabilityRating = "Low Tier";
                    else if (weeksToAfford < 8)
                        affordability.AffordabilityRating = "Mid Tier";
                    else if (weeksToAfford < 16)
                        affordability.AffordabilityRating = "High Tier";
                    else
                        affordability.AffordabilityRating = "Top Tier";
                }

                // Value rating for consumables
                if (item.IsConsumable)
                {
                    var goldPerUseRatio = affordability.CostPerUse / report.AverageGoldPerCatch;
                    affordability.ValueRating = goldPerUseRatio switch
                    {
                        <= 0.5 => "Excellent Value",
                        <= 1.0 => "Good Value",
                        <= 2.0 => "Fair Trade",
                        <= 3.0 => "Moderate Sink",
                        _ => "Significant Gold Sink"
                    };
                }

                report.ItemAffordabilityAnalysis.Add(affordability);
            }

            // Most common equipment used (from catches with boosts)
            var equipmentUsage = await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .Where(b => b.IsEquipped)
                .GroupBy(b => b.ShopItem.Name)
                .Select(g => new { ItemName = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            foreach (var equipment in equipmentUsage)
            {
                report.MostCommonEquipment[equipment.ItemName] = equipment.Count;
            }

            // Generate balance recommendations
            var recommendations = new List<string>();

            // Rarity balance check
            if (report.RarityPercentages[FishRarity.Common] > 60)
                recommendations.Add("⚠️ Common fish are too prevalent (>60%). Consider increasing rare fish drop rates or boosting equipment effectiveness.");

            if (report.RarityPercentages[FishRarity.Legendary] > 5)
                recommendations.Add("⚠️ Legendary fish may be too common (>5%). Consider reducing legendary drop rates.");

            if (report.RarityPercentages[FishRarity.Legendary] < 0.1 && report.TotalCatches > 1000)
                recommendations.Add("⚠️ Legendary fish are extremely rare (<0.1%). Consider slightly increasing drop rates for better player experience.");

            // Gold economy check
            var expensiveItems = report.ItemAffordabilityAnalysis
                .Where(i => !i.IsConsumable && i.DaysToAfford3xWeek > 60)
                .ToList();

            if (expensiveItems.Any())
                recommendations.Add($"⚠️ {expensiveItems.Count} permanent items take over 2 months to afford. Consider reducing prices or increasing gold rewards.");

            // Consumable value check
            var poorValueConsumables = report.ItemAffordabilityAnalysis
                .Where(i => i.IsConsumable && i.ValueRating.Contains("Sink"))
                .ToList();

            if (poorValueConsumables.Any())
                recommendations.Add($"💰 {poorValueConsumables.Count} consumables are expensive relative to rewards. Review pricing or boost effectiveness.");

            // Activity check
            if (report.AverageCatchesPerUser < 5 && report.TotalCatches > 50)
                recommendations.Add("📊 Low average catches per user (<5). Consider engagement campaigns or better rewards.");

            // Star distribution check
            if (report.StarPercentages.ContainsKey(3) && report.StarPercentages[3] > 50)
                recommendations.Add("⭐ 3-star catches are very common (>50%). Consider adjusting star calculation or equipment boosts.");

            if (!recommendations.Any())
                recommendations.Add("✅ Game balance looks healthy! No major issues detected.");

            report.BalanceRecommendations = recommendations;

            // Generate summary
            var summaryParts = new List<string>
            {
                $"Analyzed {report.TotalCatches:N0} catches from {report.UniqueUsers:N0} unique users.",
                $"Total gold earned: {report.TotalGoldEarned:N0} (avg {report.AverageGoldPerCatch:F1} per catch).",
                $"Most common rarity: {report.RarityDistribution.OrderByDescending(kvp => kvp.Value).First().Key} ({report.RarityPercentages[report.RarityDistribution.OrderByDescending(kvp => kvp.Value).First().Key]:F1}%).",
                $"Most caught fish: {report.MostCaughtFish ?? "N/A"}."
            };

            if (report.BoostModeActive == true)
                summaryParts.Add($"Boost mode is ACTIVE with {report.BoostModeMultiplier:F1}x multiplier.");

            report.Summary = string.Join(" ", summaryParts);

            return report;
        }
    }
}
