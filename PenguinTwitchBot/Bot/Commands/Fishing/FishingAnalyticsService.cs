using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Models;
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public class FishingAnalyticsService : IFishingAnalyticsService
    {
        private static readonly TimeSpan SessionGap = TimeSpan.FromMinutes(30);

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

        public async Task<FishingSimulationResult> SimulateFishing(int iterations, List<int> shopItemIds)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var settings = await _fishingService.GetSettings() ?? new FishingSettings();
            var boostModeActive = settings.BoostMode;
            var boostModeMultiplier = settings.BoostModeRarityMultiplier;

            var result = new FishingSimulationResult
            {
                TotalIterations = iterations,
                BoostModeUsed = boostModeActive,
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
                RemainingUses = item.MaxUses ?? -1
            }).ToList();

            result.ItemsUsed = shopItems.Select(i => i.Name).ToList();

            // Use current live settings for simulation behavior.
            var simulationSettings = new FishingSettings
            {
                BoostMode = boostModeActive,
                BoostModeRarityMultiplier = boostModeMultiplier,
                LineSnapChance = settings.LineSnapChance,
                RodSnapChance = settings.RodSnapChance,
                RarityUncommonThreshold = settings.RarityUncommonThreshold,
                RarityRareThreshold = settings.RarityRareThreshold,
                RarityEpicThreshold = settings.RarityEpicThreshold,
                RarityLegendaryThreshold = settings.RarityLegendaryThreshold
            };

            var lineSnapChance = !double.IsNaN(settings.LineSnapChance) && !double.IsInfinity(settings.LineSnapChance) &&
                settings.LineSnapChance >= 0 && settings.LineSnapChance <= 1
                ? settings.LineSnapChance
                : FishingSettings.DefaultLineSnapChance;
            var rodSnapChance = !double.IsNaN(settings.RodSnapChance) && !double.IsInfinity(settings.RodSnapChance) &&
                settings.RodSnapChance >= 0 && settings.RodSnapChance <= 1
                ? settings.RodSnapChance
                : FishingSettings.DefaultRodSnapChance;

            result.AppliedLineSnapChance = lineSnapChance;
            result.AppliedRodSnapChance = rodSnapChance;

            var totalWeight = 0.0;
            var totalGold = 0;
            var snapReplacementCost = 0.0;
            var minWeight = double.MaxValue;
            var maxWeight = 0.0;
            var heaviestFishName = string.Empty;

            // Run simulations
            for (int i = 0; i < iterations; i++)
            {
                if (StaticTools.NextDouble() < rodSnapChance)
                {
                    result.RodSnapCount++;
                    result.FailedAttempts++;
                    snapReplacementCost += ApplyRodSnapLosses(mockBoosts);
                    continue;
                }

                if (StaticTools.NextDouble() < lineSnapChance)
                {
                    result.LineSnapCount++;
                    result.FailedAttempts++;
                    snapReplacementCost += ApplyLineSnapLosses(mockBoosts);
                    continue;
                }

                var fish = FishingCalculations.SelectRandomFish(fishTypes, simulationSettings, mockBoosts);
                var stars = FishingCalculations.CalculateStars(fish, mockBoosts);
                var weight = FishingCalculations.CalculateWeight(fish, stars, mockBoosts);
                var gold = FishingCalculations.CalculateGold(fish, stars, weight);

                result.SuccessfulCatches++;

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

                ConsumeUsesAfterCatch(mockBoosts);
            }

            // Calculate statistics
            result.AverageWeight = result.SuccessfulCatches > 0
                ? Math.Round(totalWeight / result.SuccessfulCatches, 2)
                : 0;
            result.AverageGold = Math.Round((double)totalGold / iterations, 2);
            result.TotalGold = totalGold;
            result.MinWeight = result.SuccessfulCatches > 0 ? Math.Round(minWeight, 2) : 0;
            result.MaxWeight = result.SuccessfulCatches > 0 ? Math.Round(maxWeight, 2) : 0;
            result.HeaviestFish = result.SuccessfulCatches > 0 ? heaviestFishName : "None";
            result.MostCommonFish = result.FishCounts.Any()
                ? result.FishCounts.OrderByDescending(kvp => kvp.Value).First().Key
                : "None";
            result.SnapFailureRatePercent = Math.Round((double)result.FailedAttempts / iterations * 100.0, 2);
            result.SnapReplacementCostTotal = Math.Round(snapReplacementCost, 2);
            result.NetGoldAfterSnapCosts = Math.Round(totalGold - snapReplacementCost, 2);
            result.NetAverageGoldPerAttempt = Math.Round(result.NetGoldAfterSnapCosts / iterations, 2);

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

            var rarityOrder = Enum.GetValues<FishRarity>().OrderBy(r => (int)r).ToList();
            double runningTotal = 0;

            for (int i = 0; i < rarityOrder.Count; i++)
            {
                var rarity = rarityOrder[i];
                var rawPercent = rarityWeights.TryGetValue(rarity, out var weight)
                    ? (weight / totalWeight) * 100.0
                    : 0.0;

                if (i == rarityOrder.Count - 1)
                {
                    var remainder = Math.Round(100.0 - runningTotal, 4);
                    result.Probabilities[rarity] = Math.Max(0, remainder);
                }
                else
                {
                    var rounded = Math.Round(rawPercent, 4);
                    result.Probabilities[rarity] = rounded;
                    runningTotal += rounded;
                }
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
            var starProbabilities = BuildStarProbabilities(0.0);

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

            var settings = await _fishingService.GetSettings() ?? new FishingSettings();
            var lineSnapChance = !double.IsNaN(settings.LineSnapChance) && !double.IsInfinity(settings.LineSnapChance) &&
                settings.LineSnapChance >= 0 && settings.LineSnapChance <= 1
                ? settings.LineSnapChance
                : FishingSettings.DefaultLineSnapChance;
            var rodSnapChance = !double.IsNaN(settings.RodSnapChance) && !double.IsInfinity(settings.RodSnapChance) &&
                settings.RodSnapChance >= 0 && settings.RodSnapChance <= 1
                ? settings.RodSnapChance
                : FishingSettings.DefaultRodSnapChance;
            var successfulAttemptChance = (1.0 - rodSnapChance) * (1.0 - lineSnapChance);

            var shopItemCosts = await context.FishingShopItems
                .AsNoTracking()
                .ToDictionaryAsync(i => i.Name, i => i.Cost, StringComparer.OrdinalIgnoreCase);

            var tiers = BuildProgressionTiers(targetWeeks);

            var totalWeeks = tiers.Sum(t => t.Weeks);
            double weightedGold = 0.0;

            _logger.LogInformation("[PROGRESSIVE] Tier breakdown:");
            foreach (var tier in tiers)
            {
                var grossTierGold = CalculateExpectedGoldWithBoosts(fishTypes, tier.RarityBoost, tier.StarBoost, tier.WeightBoost);
                var snapReplacementCost = CalculateExpectedSnapReplacementCost(
                    tier,
                    shopItemCosts,
                    lineSnapChance,
                    rodSnapChance);
                var tierGold = Math.Max(0.0, (grossTierGold * successfulAttemptChance) - snapReplacementCost);
                var tierWeight = tier.Weeks / (double)totalWeeks;
                var contribution = tierGold * tierWeight;
                weightedGold += contribution;

                _logger.LogInformation("[PROGRESSIVE]   {Name}: {Weeks}wk, gross={Gross}g, success={Success:P2}, snapSink={Sink}g => net={Net}g × {Weight:P1} = {Contribution}g", 
                    tier.Name,
                    tier.Weeks,
                    Math.Round(grossTierGold, 2),
                    successfulAttemptChance,
                    Math.Round(snapReplacementCost, 2),
                    Math.Round(tierGold, 2),
                    tierWeight,
                    Math.Round(contribution, 2));
            }

            var result = Math.Round(weightedGold, 2);
            _logger.LogInformation("[PROGRESSIVE] Progressive baseline result: {Result}g/attempt (WITH equipment progression)", result);
            return result;
        }

        private static List<ProgressionTier> BuildProgressionTiers(int targetWeeks)
        {
            var tiers = new List<ProgressionTier>
            {
                new ProgressionTier { Name = "Naked", Weeks = 2, RarityBoost = 0.0, StarBoost = 0.0, WeightBoost = 0.0 },
                new ProgressionTier { Name = "Entry", Weeks = 5, RarityBoost = 0.05, StarBoost = 0.10, WeightBoost = 0.10, RodName = "Bamboo Rod", LineName = "Monofilament Line", HookName = "Standard Hook" },
                new ProgressionTier { Name = "Mid", Weeks = 6, RarityBoost = 0.10, StarBoost = 0.20, WeightBoost = 0.20, RodName = "Fiberglass Rod", LineName = "Braided Line", HookName = "Circle Hook" },
                new ProgressionTier { Name = "High", Weeks = 7, RarityBoost = 0.15, StarBoost = 0.30, WeightBoost = 0.30, RodName = "Carbon Fiber Rod", LineName = "Fluorocarbon Line", HookName = "Treble Hook" },
                new ProgressionTier { Name = "Top", Weeks = 6, RarityBoost = 0.25, StarBoost = 0.42, WeightBoost = 0.45, RodName = "Legendary Rod", LineName = "Titanium Wire", HookName = "Diamond Hook" }
            };

            if (targetWeeks != 26)
            {
                var scaleFactor = targetWeeks / 26.0;
                foreach (var tier in tiers)
                {
                    tier.Weeks = Math.Max(1, (int)Math.Round(tier.Weeks * scaleFactor));
                }
            }

            return tiers;
        }

        private static double CalculateWeightedSnapReplacementCostPerAttempt(
            List<ProgressionTier> tiers,
            Dictionary<string, int> shopItemCosts,
            double lineSnapChance,
            double rodSnapChance)
        {
            var totalWeeks = tiers.Sum(t => t.Weeks);
            if (totalWeeks <= 0)
            {
                return 0.0;
            }

            double weighted = 0.0;
            foreach (var tier in tiers)
            {
                var weight = tier.Weeks / (double)totalWeeks;
                var tierCost = CalculateExpectedSnapReplacementCost(tier, shopItemCosts, lineSnapChance, rodSnapChance);
                weighted += tierCost * weight;
            }

            return weighted;
        }

        private static double CalculateExpectedSnapReplacementCost(
            ProgressionTier tier,
            Dictionary<string, int> shopItemCosts,
            double lineSnapChance,
            double rodSnapChance)
        {
            if (string.IsNullOrWhiteSpace(tier.LineName) || string.IsNullOrWhiteSpace(tier.HookName))
            {
                return 0.0;
            }

            var lineCost = ResolveCost(tier.LineName, shopItemCosts, tier.Name, "line");
            var hookCost = ResolveCost(tier.HookName, shopItemCosts, tier.Name, "hook");
            var rodCost = string.IsNullOrWhiteSpace(tier.RodName)
                ? 0
                : ResolveCost(tier.RodName, shopItemCosts, tier.Name, "rod");

            var lineFailureCost = lineCost + hookCost;
            var rodFailureCost = rodCost + lineCost + hookCost;

            return ((1.0 - rodSnapChance) * lineSnapChance * lineFailureCost) + (rodSnapChance * rodFailureCost);
        }

        private static int ResolveCost(string itemName, Dictionary<string, int> shopItemCosts, string tierName, string slot)
        {
            if (shopItemCosts.TryGetValue(itemName, out var liveCost) && liveCost > 0)
            {
                return liveCost;
            }

            return tierName switch
            {
                "Entry" when slot == "rod" => 150,
                "Entry" when slot == "line" => 175,
                "Entry" when slot == "hook" => 150,
                "Mid" when slot == "rod" => 400,
                "Mid" when slot == "line" => 450,
                "Mid" when slot == "hook" => 400,
                "High" when slot == "rod" => 1000,
                "High" when slot == "line" => 1100,
                "High" when slot == "hook" => 1000,
                "Top" when slot == "rod" => 2500,
                "Top" when slot == "line" => 2800,
                "Top" when slot == "hook" => 2500,
                _ => 0
            };
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

            var starProbabilities = BuildStarProbabilities(starBoost);

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

        private static Dictionary<int, double> BuildStarProbabilities(double totalStarBoost)
        {
            // Match runtime behavior in FishingCalculations.CalculateStars() where thresholds
            // can overlap and effectively clamp at 100% because rolls are 0-100.
            var threeStarThreshold = 5.0 + (totalStarBoost * 100.0);
            var twoStarThreshold = 20.0 + (totalStarBoost * 100.0);

            var p3 = Math.Clamp(threeStarThreshold, 0.0, 100.0) / 100.0;
            var p3OrP2 = Math.Clamp(threeStarThreshold + twoStarThreshold, 0.0, 100.0) / 100.0;
            var p2 = Math.Max(0.0, p3OrP2 - p3);
            var p1 = Math.Max(0.0, 1.0 - p3 - p2);

            return new Dictionary<int, double>
            {
                { 1, p1 },
                { 2, p2 },
                { 3, p3 }
            };
        }

        private Dictionary<int, FishProbability> BuildFishProbabilityMap(
            List<FishType> fishTypes,
            bool useBoostMode,
            double boostModeMultiplier,
            List<UserFishingBoost> boosts)
        {
            var rarityWeights = CalculateRarityWeights(fishTypes, useBoostMode, boostModeMultiplier, boosts);
            var totalRarityWeight = rarityWeights.Values.Sum();
            var probabilities = new Dictionary<int, FishProbability>();

            foreach (var fish in fishTypes)
            {
                var rarityChance = rarityWeights[fish.Rarity] / totalRarityWeight;
                var fishOfRarity = fishTypes.Where(f => f.Rarity == fish.Rarity).ToList();
                var withinRarityChance = CalculateWithinRarityChance(fish, fishOfRarity, boosts);
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

        private void PopulateItemEffectPreview(
            ItemAffordability affordability,
            FishingShopItem item,
            Dictionary<int, FishProbability> baselineProbabilities,
            List<FishType> fishTypes,
            bool useBoostMode,
            double boostModeMultiplier)
        {
            var boostEntries = new List<(FishingBoostType? Type, double Amount)>
            {
                (item.BoostType, item.BoostAmount),
                (item.BoostType2, item.BoostAmount2 ?? 0),
                (item.BoostType3, item.BoostAmount3 ?? 0)
            };

            var specificBoost = boostEntries
                .Where(b => b.Type == FishingBoostType.SpecificFishBoost)
                .Sum(b => b.Amount);
            var generalBoost = boostEntries
                .Where(b => b.Type == FishingBoostType.GeneralRarityBoost)
                .Sum(b => b.Amount);
            var starBoost = boostEntries
                .Where(b => b.Type == FishingBoostType.StarBoost)
                .Sum(b => b.Amount);
            var weightBoost = boostEntries
                .Where(b => b.Type == FishingBoostType.WeightBoost)
                .Sum(b => b.Amount);

            var mockBoost = new UserFishingBoost
            {
                UserId = "analysis",
                ShopItemId = item.Id,
                ShopItem = item,
                IsEquipped = true,
                RemainingUses = 999
            };

            var withItemProbabilities = BuildFishProbabilityMap(
                fishTypes,
                useBoostMode,
                boostModeMultiplier,
                new List<UserFishingBoost> { mockBoost });

            if (specificBoost > 0 && item.TargetFishTypeId.HasValue &&
                baselineProbabilities.TryGetValue(item.TargetFishTypeId.Value, out var baselineTarget) &&
                withItemProbabilities.TryGetValue(item.TargetFishTypeId.Value, out var boostedTarget))
            {
                affordability.HasEffectPreview = true;
                affordability.EffectMetric = $"{baselineTarget.FishName} catch chance";
                affordability.EffectBaselineValue = baselineTarget.OverallChance;
                affordability.EffectWithItemValue = boostedTarget.OverallChance;
            }
            else if (generalBoost > 0)
            {
                var baselineUncommonPlus = baselineProbabilities.Values
                    .Where(p => p.Rarity != FishRarity.Common)
                    .Sum(p => p.OverallChance);
                var withItemUncommonPlus = withItemProbabilities.Values
                    .Where(p => p.Rarity != FishRarity.Common)
                    .Sum(p => p.OverallChance);

                affordability.HasEffectPreview = true;
                affordability.EffectMetric = "Uncommon+ catch chance";
                affordability.EffectBaselineValue = Math.Round(baselineUncommonPlus, 4);
                affordability.EffectWithItemValue = Math.Round(withItemUncommonPlus, 4);
            }
            else if (starBoost > 0)
            {
                var baselineThreeStar = BuildStarProbabilities(0.0)[3] * 100.0;
                var boostedThreeStar = BuildStarProbabilities(starBoost)[3] * 100.0;

                affordability.HasEffectPreview = true;
                affordability.EffectMetric = "3-star catch chance";
                affordability.EffectBaselineValue = Math.Round(baselineThreeStar, 2);
                affordability.EffectWithItemValue = Math.Round(boostedThreeStar, 2);
            }
            else if (weightBoost > 0)
            {
                affordability.HasEffectPreview = true;
                affordability.EffectMetric = "Average weight multiplier";
                affordability.EffectBaselineValue = 100.0;
                affordability.EffectWithItemValue = Math.Round((1.0 + weightBoost) * 100.0, 2);
            }

            if (affordability.HasEffectPreview && affordability.EffectBaselineValue > 0)
            {
                affordability.EffectRelativeChangePercent = Math.Round(
                    ((affordability.EffectWithItemValue / affordability.EffectBaselineValue) - 1.0) * 100.0,
                    2);
            }
        }

        private class ProgressionTier
        {
            public string Name { get; set; } = string.Empty;
            public int Weeks { get; set; }
            public double RarityBoost { get; set; }
            public double StarBoost { get; set; }
            public double WeightBoost { get; set; }
            public string? RodName { get; set; }
            public string? LineName { get; set; }
            public string? HookName { get; set; }
        }

        private static List<double> CalculateUserAverageCatchesPerSession(
            IEnumerable<(string UserId, DateTime CaughtAt)> catches,
            TimeSpan sessionGap,
            int minTotalCatches = 1,
            int minSessions = 1)
        {
            var perUserAverages = new List<double>();

            foreach (var userCatches in catches.GroupBy(c => c.UserId))
            {
                var ordered = userCatches.OrderBy(c => c.CaughtAt).ToList();
                if (!ordered.Any())
                    continue;

                var sessionCount = 1;
                var currentSessionCatches = 1;
                var totalSessionCatches = 0;
                var lastCatchTime = ordered[0].CaughtAt;

                for (var i = 1; i < ordered.Count; i++)
                {
                    var catchTime = ordered[i].CaughtAt;
                    if (catchTime - lastCatchTime > sessionGap)
                    {
                        totalSessionCatches += currentSessionCatches;
                        sessionCount++;
                        currentSessionCatches = 1;
                    }
                    else
                    {
                        currentSessionCatches++;
                    }

                    lastCatchTime = catchTime;
                }

                totalSessionCatches += currentSessionCatches;

                if (totalSessionCatches < minTotalCatches || sessionCount < minSessions)
                    continue;

                perUserAverages.Add(totalSessionCatches / (double)sessionCount);
            }

            return perUserAverages;
        }

        private static (double StreamsPerWeek, int ActiveDays, int AnalysisWindowDays) CalculateStreamsPerWeekFromAttempts(
            IEnumerable<DateTime> attemptTimestamps,
            DateTime? startDate,
            DateTime? endDate)
        {
            var attempts = attemptTimestamps
                .OrderBy(t => t)
                .ToList();

            if (!attempts.Any())
            {
                return (0, 0, 0);
            }

            var windowStart = startDate?.Date ?? attempts.First().Date;
            var windowEnd = endDate?.Date ?? attempts.Last().Date;

            if (windowEnd < windowStart)
            {
                (windowStart, windowEnd) = (windowEnd, windowStart);
            }

            var analysisWindowDays = Math.Max(1, (windowEnd - windowStart).Days + 1);
            var activeDays = attempts
                .Select(t => t.Date)
                .Distinct()
                .Count();

            var windowWeeks = analysisWindowDays / 7.0;
            if (windowWeeks <= 0)
            {
                return (0, activeDays, analysisWindowDays);
            }

            var streamsPerWeek = Math.Min(7.0, activeDays / windowWeeks);
            return (Math.Round(streamsPerWeek, 2), activeDays, analysisWindowDays);
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

            var settings = await _fishingService.GetSettings() ?? new FishingSettings();
            var lineSnapChance = !double.IsNaN(settings.LineSnapChance) && !double.IsInfinity(settings.LineSnapChance) &&
                settings.LineSnapChance >= 0 && settings.LineSnapChance <= 1
                ? settings.LineSnapChance
                : FishingSettings.DefaultLineSnapChance;
            var rodSnapChance = !double.IsNaN(settings.RodSnapChance) && !double.IsInfinity(settings.RodSnapChance) &&
                settings.RodSnapChance >= 0 && settings.RodSnapChance <= 1
                ? settings.RodSnapChance
                : FishingSettings.DefaultRodSnapChance;
            var successfulAttemptChance = (1.0 - rodSnapChance) * (1.0 - lineSnapChance);

            report.ConfiguredLineSnapChance = lineSnapChance;
            report.ConfiguredRodSnapChance = rodSnapChance;
            report.EstimatedSuccessfulAttemptRatePercent = Math.Round(successfulAttemptChance * 100.0, 2);

            // Build query for catches within date range
            var catchesQuery = context.FishCatches
                .Include(c => c.FishType)
                .AsQueryable();

            if (startDate.HasValue)
                catchesQuery = catchesQuery.Where(c => c.CaughtAt >= startDate.Value);
            if (endDate.HasValue)
                catchesQuery = catchesQuery.Where(c => c.CaughtAt <= endDate.Value);

            var catches = await catchesQuery.ToListAsync();

            // Build query for snap events within date range
            var snapEventsQuery = context.FishingSnapEvents.AsQueryable();
            if (startDate.HasValue)
                snapEventsQuery = snapEventsQuery.Where(s => s.SnappedAt >= startDate.Value);
            if (endDate.HasValue)
                snapEventsQuery = snapEventsQuery.Where(s => s.SnappedAt <= endDate.Value);

            var snapEvents = await snapEventsQuery.ToListAsync();

            var attemptSamples = catches
                .Select(c => new { c.UserId, Timestamp = c.CaughtAt, IsCatch = true })
                .Concat(snapEvents.Select(s => new { s.UserId, Timestamp = s.SnappedAt, IsCatch = false }))
                .OrderBy(a => a.Timestamp)
                .ToList();

            // Basic statistics
            report.TotalCatches = catches.Count;
            report.UniqueUsers = attemptSamples.Any()
                ? attemptSamples.Select(a => a.UserId).Distinct().Count()
                : catches.Select(c => c.UserId).Distinct().Count();

            var observedAttempts = attemptSamples.Count;
            var hasObservedAttemptData = observedAttempts > 0;
            var hasObservedSnapData = snapEvents.Count > 0;

            report.AttemptDataSource = hasObservedSnapData
                ? "Observed attempts (catches + recorded snaps)"
                : "Estimated attempts from catch success model (no snap telemetry in range)";

            report.EstimatedTotalAttempts = hasObservedSnapData
                ? observedAttempts
                : (successfulAttemptChance > 0
                    ? (int)Math.Round(report.TotalCatches / successfulAttemptChance)
                    : report.TotalCatches);
            report.EstimatedFailedAttempts = Math.Max(0, report.EstimatedTotalAttempts - report.TotalCatches);

            if (hasObservedSnapData)
            {
                report.EstimatedLineSnaps = snapEvents.Count(s => string.Equals(s.SnapType, "Line", StringComparison.OrdinalIgnoreCase));
                report.EstimatedRodSnaps = snapEvents.Count(s => string.Equals(s.SnapType, "Rod", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                report.EstimatedLineSnaps = (int)Math.Round(report.EstimatedTotalAttempts * (1.0 - rodSnapChance) * lineSnapChance);
                report.EstimatedRodSnaps = (int)Math.Round(report.EstimatedTotalAttempts * rodSnapChance);
            }

            var observedSuccessChance = report.EstimatedTotalAttempts > 0
                ? report.TotalCatches / (double)report.EstimatedTotalAttempts
                : successfulAttemptChance;
            report.EstimatedSuccessfulAttemptRatePercent = Math.Round(observedSuccessChance * 100.0, 2);

            var (streamsPerWeekFromAttempts, _, analysisWindowDays) = CalculateStreamsPerWeekFromAttempts(
                attemptSamples.Select(a => a.Timestamp),
                startDate,
                endDate);
            var analysisWindowWeeks = Math.Max(analysisWindowDays / 7.0, 1.0 / 7.0);

            report.StreamsPerWeekFromAttempts = streamsPerWeekFromAttempts;
            report.AttemptsPerWeek = Math.Round(report.EstimatedTotalAttempts / analysisWindowWeeks, 2);
            report.CatchesPerWeek = Math.Round(report.TotalCatches / analysisWindowWeeks, 2);

            var shopItemCosts = await context.FishingShopItems
                .AsNoTracking()
                .ToDictionaryAsync(i => i.Name, i => i.Cost, StringComparer.OrdinalIgnoreCase);
            var progressionTiers = BuildProgressionTiers(26);
            var weightedSnapCostPerAttempt = CalculateWeightedSnapReplacementCostPerAttempt(
                progressionTiers,
                shopItemCosts,
                lineSnapChance,
                rodSnapChance);

            var observedSnapCostTotal = snapEvents.Sum(s => (double)s.TotalGoldLost);
            var observedSnapCostPerAttempt = report.EstimatedTotalAttempts > 0
                ? observedSnapCostTotal / report.EstimatedTotalAttempts
                : 0;
            var selectedSnapCostPerAttempt = hasObservedSnapData
                ? observedSnapCostPerAttempt
                : weightedSnapCostPerAttempt;

            report.EstimatedSnapReplacementCostPerAttempt = Math.Round(selectedSnapCostPerAttempt, 2);
            report.EstimatedSnapReplacementCostTotal = Math.Round(selectedSnapCostPerAttempt * report.EstimatedTotalAttempts, 2);

            if (report.TotalCatches == 0)
            {
                report.SnapAdjustedAverageGoldPerAttempt = Math.Round(0.0 - selectedSnapCostPerAttempt, 2);
                report.SnapAdjustedMedianGoldPerAttempt = Math.Round(0.0 - selectedSnapCostPerAttempt, 2);
                report.SnapAdjustedTotalNetGold = Math.Round(0.0 - report.EstimatedSnapReplacementCostTotal, 2);
                report.BalanceRecommendations = ["No catches found in the specified date range."];
                return report;
            }

            // Gold economics
            var goldValues = catches.Select(c => c.GoldEarned).OrderBy(g => g).ToList();
            report.TotalGoldEarned = goldValues.Sum();
            report.AverageGoldPerCatch = Math.Round((double)report.TotalGoldEarned / report.TotalCatches, 2);
            report.MedianGoldPerCatch = goldValues.Count % 2 == 0
                ? (goldValues[goldValues.Count / 2 - 1] + goldValues[goldValues.Count / 2]) / 2.0
                : goldValues[goldValues.Count / 2];
            report.SnapAdjustedAverageGoldPerAttempt = Math.Round((report.AverageGoldPerCatch * observedSuccessChance) - selectedSnapCostPerAttempt, 2);
            report.SnapAdjustedMedianGoldPerAttempt = Math.Round((report.MedianGoldPerCatch * observedSuccessChance) - selectedSnapCostPerAttempt, 2);
            report.SnapAdjustedTotalNetGold = Math.Round(report.TotalGoldEarned - report.EstimatedSnapReplacementCostTotal, 2);

            var userGroups = catches.GroupBy(c => c.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    CatchCount = g.Count(),
                    TotalGold = g.Sum(c => c.GoldEarned)
                })
                .ToList();

            // Calculate real engagement tiers from timestamped ATTEMPTS per session (catches + snaps)
            var userAttemptSessionAverages = CalculateUserAverageCatchesPerSession(
                attemptSamples.Select(a => (a.UserId, a.Timestamp)),
                SessionGap,
                minTotalCatches: 5,
                minSessions: 2);

            if (!userAttemptSessionAverages.Any())
            {
                userAttemptSessionAverages = CalculateUserAverageCatchesPerSession(
                    attemptSamples.Select(a => (a.UserId, a.Timestamp)),
                    SessionGap);
            }

            userAttemptSessionAverages = userAttemptSessionAverages
                .OrderBy(v => v)
                .ToList();

            // Use percentiles to define player engagement types.
            double casualAttemptsPerSession = 15.0;
            double activeAttemptsPerSession = 30.0;
            double hardcoreAttemptsPerSession = 100.0;

            if (userAttemptSessionAverages.Count > 0)
            {
                var p25Index = (int)Math.Ceiling(userAttemptSessionAverages.Count * 0.25) - 1;
                casualAttemptsPerSession = Math.Max(userAttemptSessionAverages[Math.Max(0, p25Index)], 1.0);

                var p50Index = userAttemptSessionAverages.Count / 2;
                activeAttemptsPerSession = userAttemptSessionAverages.Count % 2 == 0
                    ? (userAttemptSessionAverages[p50Index - 1] + userAttemptSessionAverages[p50Index]) / 2.0
                    : userAttemptSessionAverages[p50Index];

                var p75Index = (int)Math.Ceiling(userAttemptSessionAverages.Count * 0.75) - 1;
                hardcoreAttemptsPerSession = userAttemptSessionAverages[Math.Min(p75Index, userAttemptSessionAverages.Count - 1)];
            }

            report.CasualAttemptsPerSession = Math.Round(casualAttemptsPerSession, 1);
            report.ActiveAttemptsPerSession = Math.Round(activeAttemptsPerSession, 1);
            report.HardcoreAttemptsPerSession = Math.Round(hardcoreAttemptsPerSession, 1);

            // Item affordability analysis
            var shopItems = await context.FishingShopItems
                .Include(i => i.TargetFishType)
                .Where(i => i.Enabled)
                .ToListAsync();
            var enabledFishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            var userGoldTotals = userGroups.Select(u => u.TotalGold).OrderBy(g => g).ToList();
            var medianUserGold = userGoldTotals.Count > 0
                ? (userGoldTotals.Count % 2 == 0
                    ? (userGoldTotals[userGoldTotals.Count / 2 - 1] + userGoldTotals[userGoldTotals.Count / 2]) / 2.0
                    : userGoldTotals[userGoldTotals.Count / 2])
                : 0;

            var useBoostMode = settings?.BoostMode ?? false;
            var boostModeMultiplier = settings?.BoostModeRarityMultiplier ?? 1.0;
            var baselineProbabilities = BuildFishProbabilityMap(
                enabledFishTypes,
                useBoostMode,
                boostModeMultiplier,
                new List<UserFishingBoost>());

            var effectiveGoldPerAttempt = Math.Max(0, report.SnapAdjustedAverageGoldPerAttempt);

            foreach (var item in shopItems)
            {
                var affordability = new ItemAffordability
                {
                    ItemName = item.Name,
                    Cost = item.Cost,
                    IsConsumable = item.IsConsumable,
                    MaxUses = item.MaxUses,
                    EquipmentSlot = item.EquipmentSlot?.ToString() ?? "None",
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
                affordability.AttemptsNeededToBuy = effectiveGoldPerAttempt > 0
                    ? Math.Round(item.Cost / effectiveGoldPerAttempt, 1)
                    : 0;

                // Sessions to afford based on REAL attempt/session engagement percentiles.
                affordability.SessionsToAffordCasual = affordability.AttemptsNeededToBuy > 0
                    ? Math.Round(affordability.AttemptsNeededToBuy / casualAttemptsPerSession, 1)
                    : 0;

                affordability.SessionsToAffordActive = affordability.AttemptsNeededToBuy > 0
                    ? Math.Round(affordability.AttemptsNeededToBuy / activeAttemptsPerSession, 1)
                    : 0;

                affordability.SessionsToAffordHardcore = affordability.AttemptsNeededToBuy > 0
                    ? Math.Round(affordability.AttemptsNeededToBuy / hardcoreAttemptsPerSession, 1)
                    : 0;

                // Affordability rating for permanent items
                // Based on sessions needed for active players (median engagement, 2-3 sessions/week)
                // Starter=<2 sessions, Low=2-5 sessions, Mid=5-10 sessions, High=10-20 sessions, Endgame=20+ sessions
                if (!item.IsConsumable)
                {
                    var sessionsNeeded = affordability.SessionsToAffordActive;

                    if (sessionsNeeded < 1)
                        affordability.AffordabilityRating = "Too Cheap";
                    else if (sessionsNeeded < 2)
                        affordability.AffordabilityRating = "Starter Tier";
                    else if (sessionsNeeded < 5)
                        affordability.AffordabilityRating = "Low Tier";
                    else if (sessionsNeeded < 10)
                        affordability.AffordabilityRating = "Mid Tier";
                    else if (sessionsNeeded < 20)
                        affordability.AffordabilityRating = "High Tier";
                    else
                        affordability.AffordabilityRating = "Endgame Tier";
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

                PopulateItemEffectPreview(
                    affordability,
                    item,
                    baselineProbabilities,
                    enabledFishTypes,
                    useBoostMode,
                    boostModeMultiplier);

                report.ItemAffordabilityAnalysis.Add(affordability);
            }

            var topGearItems = shopItems
                .Where(i => i.Enabled && !i.IsConsumable && i.EquipmentSlot.HasValue &&
                            i.EquipmentSlot != EquipmentSlot.Bait && i.EquipmentSlot != EquipmentSlot.Lure)
                .GroupBy(i => i.EquipmentSlot!.Value)
                .Select(g => g.OrderByDescending(i => i.Cost).First())
                .ToList();

            report.TopGearTotalCost = topGearItems.Sum(i => i.Cost);

            var projectedSuccessRate = report.EstimatedTotalAttempts > 0
                ? report.TotalCatches / (double)report.EstimatedTotalAttempts
                : 0;

            var projectionWindows = new[]
            {
                (Weeks: 12, Label: "3 Months"),
                (Weeks: 26, Label: "6 Months"),
                (Weeks: 52, Label: "12 Months")
            };

            foreach (var window in projectionWindows)
            {
                var projection = new BalanceProjectionWindow
                {
                    Weeks = window.Weeks,
                    Label = window.Label
                };

                foreach (var tier in new[]
                {
                    (Name: "Casual", AttemptsPerSession: report.CasualAttemptsPerSession),
                    (Name: "Active", AttemptsPerSession: report.ActiveAttemptsPerSession),
                    (Name: "Hardcore", AttemptsPerSession: report.HardcoreAttemptsPerSession)
                })
                {
                    var attemptsPerWeek = tier.AttemptsPerSession * report.StreamsPerWeekFromAttempts;
                    var projectedAttempts = attemptsPerWeek * window.Weeks;
                    var projectedCatches = projectedAttempts * projectedSuccessRate;
                    var projectedGrossGold = projectedCatches * report.AverageGoldPerCatch;
                    var projectedSnapSink = projectedAttempts * report.EstimatedSnapReplacementCostPerAttempt;
                    var projectedNetGold = Math.Max(0, projectedGrossGold - projectedSnapSink);
                    var maxGearProgress = report.TopGearTotalCost > 0
                        ? Math.Min(999.0, (projectedNetGold / report.TopGearTotalCost) * 100.0)
                        : 0;

                    projection.Tiers.Add(new BalanceProjectionTier
                    {
                        TierName = tier.Name,
                        AttemptsPerSession = Math.Round(tier.AttemptsPerSession, 2),
                        StreamsPerWeek = report.StreamsPerWeekFromAttempts,
                        AttemptsPerWeek = Math.Round(attemptsPerWeek, 2),
                        ProjectedAttempts = Math.Round(projectedAttempts, 1),
                        ProjectedCatches = Math.Round(projectedCatches, 1),
                        ProjectedGrossGold = Math.Round(projectedGrossGold, 1),
                        ProjectedSnapSink = Math.Round(projectedSnapSink, 1),
                        ProjectedNetGold = Math.Round(projectedNetGold, 1),
                        MaxGearProgressPercent = Math.Round(maxGearProgress, 1)
                    });
                }

                report.ProjectionWindows.Add(projection);
            }

            // Generate balance recommendations
            var recommendations = new List<string>();

            if (report.SnapAdjustedAverageGoldPerAttempt <= 0)
                recommendations.Add("?? Snap-adjusted net gold/attempt is non-positive. Lower snap rates or reduce rod/line/hook prices.");

            if (report.EstimatedFailedAttempts > 0 && report.EstimatedTotalAttempts > 0)
            {
                var failPct = Math.Round((double)report.EstimatedFailedAttempts / report.EstimatedTotalAttempts * 100.0, 2);
                if (failPct > 5.0)
                {
                    recommendations.Add($"?? Estimated snap failure rate is {failPct:F2}%. Consider lowering line/rod snap chances.");
                }
            }

            // Gold economy check - items taking more than 20 sessions for active players.
            var expensiveItems = report.ItemAffordabilityAnalysis
                .Where(i => !i.IsConsumable && i.SessionsToAffordActive > 20)
                .ToList();

            if (expensiveItems.Any())
                recommendations.Add($"?? {expensiveItems.Count} permanent items take over 20 active sessions. At {report.StreamsPerWeekFromAttempts:F2} streams/week this can be very slow progression.");

            // Check for progression that's TOO fast
            var tooFastItems = report.ItemAffordabilityAnalysis
                .Where(i => !i.IsConsumable && i.SessionsToAffordActive < 1 && i.Cost > 100)
                .ToList();

            if (tooFastItems.Any())
                recommendations.Add($"?? {tooFastItems.Count} permanent items can be bought in less than 1 session. Consider increasing prices to extend progression.");

            // Consumable value check
            var poorValueConsumables = report.ItemAffordabilityAnalysis
                .Where(i => i.IsConsumable && i.ValueRating.Contains("Sink"))
                .ToList();

            if (poorValueConsumables.Any())
                recommendations.Add($"?? {poorValueConsumables.Count} consumables are expensive relative to rewards. Review pricing or boost effectiveness.");

            if (!recommendations.Any())
                recommendations.Add("? Game balance looks healthy! No major issues detected.");

            report.BalanceRecommendations = recommendations;
            return report;
        }

        public async Task<Dictionary<string, int>> CalculateRecommendedPricing(int targetWeeksForEndgame = 26)
        {
            _logger.LogInformation("[PRICING] CalculateRecommendedPricing() CALLED - targetWeeksForEndgame: {Weeks}", targetWeeksForEndgame);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var catchSamples = await context.FishCatches
                .Select(c => new { c.UserId, Timestamp = c.CaughtAt })
                .ToListAsync();

            var snapSamples = await context.FishingSnapEvents
                .Select(s => new { s.UserId, Timestamp = s.SnappedAt })
                .ToListAsync();

            var attemptSamples = catchSamples
                .Select(c => new { c.UserId, c.Timestamp })
                .Concat(snapSamples.Select(s => new { s.UserId, s.Timestamp }))
                .OrderBy(a => a.Timestamp)
                .ToList();

            if (!attemptSamples.Any())
            {
                _logger.LogWarning("[PRICING] No attempt data available, returning empty recommendations");
                return new Dictionary<string, int>();
            }

            var userSessionAverages = CalculateUserAverageCatchesPerSession(
                attemptSamples.Select(c => (c.UserId, c.Timestamp)),
                SessionGap,
                minTotalCatches: 5,
                minSessions: 2);

            if (!userSessionAverages.Any())
            {
                userSessionAverages = CalculateUserAverageCatchesPerSession(
                    attemptSamples.Select(c => (c.UserId, c.Timestamp)),
                    SessionGap);
            }

            userSessionAverages = userSessionAverages
                .OrderBy(v => v)
                .ToList();

            double activeAttemptsPerSession = 30.0; // Fallback
            if (userSessionAverages.Count > 0)
            {
                var p50Index = userSessionAverages.Count / 2;
                activeAttemptsPerSession = userSessionAverages.Count % 2 == 0
                    ? (userSessionAverages[p50Index - 1] + userSessionAverages[p50Index]) / 2.0
                    : userSessionAverages[p50Index];
            }

            var (streamsPerWeek, _, _) = CalculateStreamsPerWeekFromAttempts(
                attemptSamples.Select(a => a.Timestamp),
                null,
                null);

            if (streamsPerWeek <= 0)
            {
                streamsPerWeek = 1.0;
            }

            // Calculate progressive baseline gold (with equipment progression over time)
            var expectedGoldPerCatch = await CalculateProgressiveBaselineGold(targetWeeksForEndgame);

            _logger.LogInformation("[PRICING] Active player engagement: {Attempts} attempts/session", Math.Round(activeAttemptsPerSession, 1));
            _logger.LogInformation("[PRICING] Observed streams/week from attempts: {StreamsPerWeek}", Math.Round(streamsPerWeek, 2));
            _logger.LogInformation("[PRICING] Expected gold per catch: {Gold}g (with progression)", Math.Round(expectedGoldPerCatch, 2));

            // Define progression tier targets (in weeks, scaled to endgame horizon)
            // Scale weeks based on targetWeeksForEndgame parameter
            var scaleFactor = targetWeeksForEndgame / 26.0; // Default is 26 weeks for endgame

            var pricingTiers = new List<(string Name, int TargetWeeks)>
            {
                ("Entry", (int)Math.Round(2 * scaleFactor)),      // ~8% of progression (2/26)
                ("Mid", (int)Math.Round(6 * scaleFactor)),         // ~23% of progression (6/26)
                ("High", (int)Math.Round(12 * scaleFactor)),       // ~46% of progression (12/26)
                ("Top", targetWeeksForEndgame)                      // 100% - endgame
            };

            var recommendations = new Dictionary<string, int>();

            _logger.LogInformation("[PRICING] Tier targets: Entry={EntryWeeks}w, Mid={MidWeeks}w, High={HighWeeks}w, Top={TopWeeks}w",
                pricingTiers[0].TargetWeeks, pricingTiers[1].TargetWeeks, pricingTiers[2].TargetWeeks, pricingTiers[3].TargetWeeks);

            // Define item distribution across tiers
            var itemTiers = new Dictionary<string, string>
            {
                // Entry Tier Rods
                { "Bamboo Rod", "Entry" },
                // Entry Tier Reels
                { "Basic Reel", "Entry" },
                // Entry Tier Lines
                { "Monofilament Line", "Entry" },
                // Entry Tier Hooks
                { "Standard Hook", "Entry" },
                // Entry Tier Tackle Boxes
                { "Basic Tackle Box", "Entry" },
                // Entry Tier Nets
                { "Landing Net", "Entry" },

                // Mid Tier Rods
                { "Fiberglass Rod", "Mid" },
                // Mid Tier Reels
                { "Precision Reel", "Mid" },
                // Mid Tier Lines
                { "Braided Line", "Mid" },
                // Mid Tier Hooks
                { "Circle Hook", "Mid" },
                // Mid Tier Tackle Boxes
                { "Pro Tackle Box", "Mid" },
                // Mid Tier Nets
                { "Knotless Net", "Mid" },

                // High Tier Rods
                { "Carbon Fiber Rod", "High" },
                // High Tier Reels
                { "Professional Reel", "High" },
                // High Tier Lines
                { "Fluorocarbon Line", "High" },
                // High Tier Hooks
                { "Treble Hook", "High" },
                // High Tier Tackle Boxes
                { "Master Tackle Box", "High" },
                // High Tier Nets
                { "Tournament Net", "High" },

                // Top Tier Rods
                { "Legendary Rod", "Top" },
                // Top Tier Reels
                { "Master Reel", "Top" },
                // Top Tier Lines
                { "Titanium Wire", "Top" },
                // Top Tier Hooks
                { "Diamond Hook", "Top" }
            };

            // Calculate prices for each item based on tier's target weeks
            foreach (var (itemName, tierName) in itemTiers)
            {
                var tier = pricingTiers.FirstOrDefault(t => t.Name == tierName);
                if (tier == default)
                {
                    _logger.LogWarning("[PRICING] Unknown tier '{Tier}' for item '{Item}'", tierName, itemName);
                    continue;
                }

                // Calculate price: sessions × catches/session × gold/catch
                var sessionsNeeded = tier.TargetWeeks * streamsPerWeek;
                var catchesNeeded = sessionsNeeded * activeAttemptsPerSession;
                var targetPrice = (int)Math.Round(catchesNeeded * expectedGoldPerCatch);

                recommendations[itemName] = targetPrice;

                _logger.LogDebug("[PRICING] {Item} ({Tier}): {Price}g = {Weeks}w × {SessionsPerWeek} × {CatchesPerSession} × {GoldPerCatch}g",
                    itemName, tierName, targetPrice, tier.TargetWeeks, streamsPerWeek, Math.Round(activeAttemptsPerSession, 1), Math.Round(expectedGoldPerCatch, 2));
            }

            _logger.LogInformation("[PRICING] Generated {Count} pricing recommendations based on real engagement data",
                recommendations.Count);

            return recommendations;
        }

        private static double ApplyLineSnapLosses(List<UserFishingBoost> boosts)
        {
            var replacementCost = 0.0;

            // Charge replacement for line/hook every time they snap,
            // but keep them in the simulated loadout so future snaps can still incur cost.
            foreach (var item in boosts.Where(b => b.ShopItem?.EquipmentSlot == EquipmentSlot.Line || b.ShopItem?.EquipmentSlot == EquipmentSlot.Hook))
            {
                replacementCost += item.ShopItem?.Cost ?? 0;
            }

            var baitLureItems = boosts
                .Where(b => b.ShopItem?.EquipmentSlot == EquipmentSlot.Bait || b.ShopItem?.EquipmentSlot == EquipmentSlot.Lure)
                .ToList();

            foreach (var item in baitLureItems)
            {
                if (item.RemainingUses == -1)
                {
                    // Unlimited bait/lure are lost and replaced on snap.
                    replacementCost += item.ShopItem?.Cost ?? 0;
                    continue;
                }

                if (item.RemainingUses > 0)
                {
                    item.RemainingUses--;

                    var maxUses = item.ShopItem?.MaxUses ?? 1;
                    var perUseCost = maxUses > 0
                        ? (item.ShopItem?.Cost ?? 0) / (double)maxUses
                        : item.ShopItem?.Cost ?? 0;

                    replacementCost += perUseCost;
                }

                if (item.RemainingUses <= 0)
                {
                    boosts.Remove(item);
                }
            }

            return replacementCost;
        }

        private static double ApplyRodSnapLosses(List<UserFishingBoost> boosts)
        {
            var replacementCost = 0.0;

            // Charge replacement for rod every time it snaps,
            // but keep it in simulated loadout for future attempts.
            foreach (var rod in boosts.Where(b => b.ShopItem?.EquipmentSlot == EquipmentSlot.Rod))
            {
                replacementCost += rod.ShopItem?.Cost ?? 0;
            }

            replacementCost += ApplyLineSnapLosses(boosts);
            return replacementCost;
        }

        private static void ConsumeUsesAfterCatch(List<UserFishingBoost> boosts)
        {
            var removable = new List<UserFishingBoost>();

            foreach (var boost in boosts)
            {
                if (boost.RemainingUses == -1)
                {
                    continue;
                }

                if (boost.RemainingUses > 0)
                {
                    boost.RemainingUses--;
                }

                if (boost.RemainingUses <= 0)
                {
                    removable.Add(boost);
                }
            }

            foreach (var expired in removable)
            {
                boosts.Remove(expired);
            }
        }
    }
}
