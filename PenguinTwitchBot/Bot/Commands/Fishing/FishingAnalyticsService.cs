using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

            foreach (FishRarity rarity in Enum.GetValues(typeof(FishRarity)))
            {
                result.RarityCounts[rarity] = 0;
            }
            result.StarCounts[1] = 0;
            result.StarCounts[2] = 0;
            result.StarCounts[3] = 0;

            var fishTypes = await context.FishTypes.AsNoTracking().Include(f => f.Categories).Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                throw new InvalidOperationException("No fish types available for simulation");
            }

            var shopItems = await context.FishingShopItems
                .AsNoTracking()
                .Where(i => shopItemIds.Contains(i.Id))
                .ToListAsync();

            var mockBoosts = shopItems.Select(item => new UserFishingBoost
            {
                UserId = "simulation",
                ShopItemId = item.Id,
                ShopItem = item,
                IsEquipped = true,
                RemainingUses = item.MaxUses ?? -1,
                CurrentDurability = item.MaxDurability.HasValue ? (double)item.MaxDurability.Value : null
            }).ToList();

            result.ItemsUsed = shopItems.Select(i => i.Name).ToList();

            var simulationSettings = new FishingSettings
            {
                BoostMode = boostModeActive,
                BoostModeRarityMultiplier = boostModeMultiplier,
                LineSnapChance = settings.LineSnapChance,
                RodSnapChance = settings.RodSnapChance,
                ReelJamChance = settings.ReelJamChance,
                TackleBoxLostChance = settings.TackleBoxLostChance,
                NetBreakChance = settings.NetBreakChance,
                RepairCostMultiplier = settings.RepairCostMultiplier,
                RarityUncommonThreshold = settings.RarityUncommonThreshold,
                RarityRareThreshold = settings.RarityRareThreshold,
                RarityEpicThreshold = settings.RarityEpicThreshold,
                RarityLegendaryThreshold = settings.RarityLegendaryThreshold,
                RarityMythicalThreshold = settings.RarityMythicalThreshold
            };

            var lineSnapChance = Math.Clamp(settings.LineSnapChance, 0.0, 1.0);
            var rodSnapChance = Math.Clamp(settings.RodSnapChance, 0.0, 1.0);
            var reelJamChance = Math.Clamp(settings.ReelJamChance, 0.0, 1.0);
            var tackleBoxLostChance = Math.Clamp(settings.TackleBoxLostChance, 0.0, 1.0);
            var netBreakChance = Math.Clamp(settings.NetBreakChance, 0.0, 1.0);

            result.AppliedLineSnapChance = lineSnapChance;
            result.AppliedRodSnapChance = rodSnapChance;

            var totalWeight = 0.0;
            var totalGold = 0;
            var snapReplacementCost = 0.0;
            var minWeight = double.MaxValue;
            var maxWeight = 0.0;
            var heaviestFishName = string.Empty;

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

                if (StaticTools.NextDouble() < reelJamChance)
                {
                    result.FailedAttempts++;
                    snapReplacementCost += ApplySlotLoss(mockBoosts, EquipmentSlot.Reel);
                    continue;
                }

                if (StaticTools.NextDouble() < tackleBoxLostChance)
                {
                    result.FailedAttempts++;
                    snapReplacementCost += ApplySlotLoss(mockBoosts, EquipmentSlot.TackleBox);
                    continue;
                }

                if (StaticTools.NextDouble() < netBreakChance)
                {
                    result.FailedAttempts++;
                    snapReplacementCost += ApplySlotLoss(mockBoosts, EquipmentSlot.Net);
                    continue;
                }

                var fish = FishingCalculations.SelectRandomFish(fishTypes, simulationSettings, mockBoosts);
                var stars = FishingCalculations.CalculateStars(fish, mockBoosts);
                var weight = FishingCalculations.CalculateWeight(fish, stars, mockBoosts);
                var gold = FishingCalculations.CalculateGold(fish, stars, weight);

                result.SuccessfulCatches++;
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

            var fishTypes = await context.FishTypes.AsNoTracking().Include(f => f.Categories).Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                return new Dictionary<int, FishProbability>();
            }

            var shopItems = await context.FishingShopItems
                .AsNoTracking()
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
            var totalRarityWeight = rarityWeights.Values.Sum();
            var probabilities = new Dictionary<int, FishProbability>();

            foreach (var fish in fishTypes)
            {
                var rarityChance = rarityWeights[fish.Rarity] / totalRarityWeight;
                var fishOfRarity = fishTypes.Where(f => f.Rarity == fish.Rarity).ToList();
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

            var fishTypes = await context.FishTypes.AsNoTracking().Include(f => f.Categories).Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                return new RarityProbability();
            }

            var shopItems = await context.FishingShopItems
                .AsNoTracking()
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

            var result = new RarityProbability();
            var rarityOrder = new[]
            {
                FishRarity.Common,
                FishRarity.Uncommon,
                FishRarity.Rare,
                FishRarity.Epic,
                FishRarity.Legendary,
                FishRarity.Mythical
            };

            foreach (var rarity in rarityOrder)
            {
                result.Probabilities[rarity] = 0.0;
            }

            var presentRarities = rarityOrder.Where(rarityWeights.ContainsKey).ToList();
            if (presentRarities.Count == 0 || totalWeight <= 0)
            {
                return result;
            }

            double runningTotal = 0;
            for (int i = 0; i < presentRarities.Count; i++)
            {
                var rarity = presentRarities[i];
                var rawPercent = (rarityWeights[rarity] / totalWeight) * 100.0;

                if (i == presentRarities.Count - 1)
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

        public async Task<double> CalculateBaselineExpectedGold()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fishTypes = await context.FishTypes.AsNoTracking().Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                return 0.0;
            }

            var rarityWeights = FishingRarityWeightProfiles.CreateAvailableWeights(fishTypes);
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

            return Math.Round(expectedGold, 2);
        }

        public async Task<double> CalculateProgressiveBaselineGold(int targetWeeks = 26)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fishTypes = await context.FishTypes.AsNoTracking().Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                return 0.0;
            }

            var settings = await _fishingService.GetSettings() ?? new FishingSettings();
            var lineSnapChance = Math.Clamp(settings.LineSnapChance, 0.0, 1.0);
            var rodSnapChance = Math.Clamp(settings.RodSnapChance, 0.0, 1.0);
            var reelJamChance = Math.Clamp(settings.ReelJamChance, 0.0, 1.0);
            var tackleBoxLostChance = Math.Clamp(settings.TackleBoxLostChance, 0.0, 1.0);
            var netBreakChance = Math.Clamp(settings.NetBreakChance, 0.0, 1.0);
            var repairCostMultiplier = Math.Max(0.0, settings.RepairCostMultiplier);

            var successfulAttemptChance = (1.0 - rodSnapChance) * (1.0 - lineSnapChance) * (1.0 - reelJamChance) * (1.0 - tackleBoxLostChance) * (1.0 - netBreakChance);

            var shopItems = await context.FishingShopItems.AsNoTracking().ToListAsync();
            var tiers = BuildProgressionTiers(targetWeeks);
            var totalWeeks = tiers.Sum(t => t.Weeks);
            double weightedGold = 0.0;

            foreach (var tier in tiers)
            {
                var grossTierGold = CalculateExpectedGoldWithBoosts(fishTypes, tier.RarityBoost, tier.StarBoost, tier.WeightBoost);
                var tierItems = ResolveTierItems(tier.Name, shopItems);

                var accidentSink = CalculateExpectedAccidentSink(
                    tierItems,
                    lineSnapChance,
                    rodSnapChance,
                    reelJamChance,
                    tackleBoxLostChance,
                    netBreakChance);

                var upkeepSink = CalculateExpectedDurabilityUpkeep(tierItems, repairCostMultiplier);

                var tierGold = Math.Max(0.0, (grossTierGold * successfulAttemptChance) - accidentSink - upkeepSink);
                var tierWeight = totalWeeks > 0 ? tier.Weeks / (double)totalWeeks : 0;
                weightedGold += tierGold * tierWeight;
            }

            return Math.Round(weightedGold, 2);
        }

        public async Task<ProjectedEconomyModel> CalculateProjectedEconomy()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fishTypes = await context.FishTypes.AsNoTracking().Include(f => f.Categories).Where(f => f.Enabled).ToListAsync();
            var shopItems = await context.FishingShopItems.AsNoTracking().ToListAsync();
            var settings = await _fishingService.GetSettings() ?? new FishingSettings();

            var lineSnap = Math.Clamp(settings.LineSnapChance, 0.0, 1.0);
            var rodSnap = Math.Clamp(settings.RodSnapChance, 0.0, 1.0);
            var reelJam = Math.Clamp(settings.ReelJamChance, 0.0, 1.0);
            var tackleLost = Math.Clamp(settings.TackleBoxLostChance, 0.0, 1.0);
            var netBreak = Math.Clamp(settings.NetBreakChance, 0.0, 1.0);
            var repairMultiplier = Math.Max(0.0, settings.RepairCostMultiplier);

            var successRate = (1.0 - rodSnap) * (1.0 - lineSnap) * (1.0 - reelJam) * (1.0 - tackleLost) * (1.0 - netBreak);
            var baselineGrossGoldPerCatch = await CalculateBaselineExpectedGold();
            var baselineGrossGoldPerAttempt = Math.Round(baselineGrossGoldPerCatch * successRate, 2);

            var model = new ProjectedEconomyModel
            {
                BaselineGrossGoldPerCatch = baselineGrossGoldPerCatch,
                BaselineGrossGoldPerAttempt = baselineGrossGoldPerAttempt,
                BaselineSuccessRatePercent = Math.Round(successRate * 100.0, 2)
            };

            var tierNames = new[] { "Bare Hands", "Entry", "Mid", "High", "Top" };
            foreach (var tierName in tierNames)
            {
                var items = ResolveTierItems(tierName, shopItems);
                var rarityBoost = 0.0;
                var starBoost = 0.0;
                var weightBoost = 0.0;

                foreach (var item in items)
                {
                    AccumulateBoosts(item, ref rarityBoost, ref starBoost, ref weightBoost);
                }

                var grossCatch = fishTypes.Any()
                    ? CalculateExpectedGoldWithBoosts(fishTypes, rarityBoost, starBoost, weightBoost)
                    : baselineGrossGoldPerCatch;

                var grossAttempt = Math.Round(grossCatch * successRate, 2);
                var upkeep = Math.Round(CalculateExpectedDurabilityUpkeep(items, repairMultiplier), 2);
                var accidentSink = Math.Round(CalculateExpectedAccidentSink(items, lineSnap, rodSnap, reelJam, tackleLost, netBreak), 2);
                var consumableSink = Math.Round(CalculateExpectedConsumableSink(items), 2);
                var netAttempt = Math.Round(grossAttempt - upkeep - accidentSink - consumableSink, 2);
                var margin = grossAttempt > 0 ? Math.Round((netAttempt / grossAttempt) * 100.0, 1) : 0;

                model.TierEconomics.Add(new TierProjectedEconomics
                {
                    TierName = tierName,
                    TotalLoadoutCost = items.Sum(i => i.Cost),
                    SuccessRatePercent = Math.Round(successRate * 100.0, 2),
                    GrossGoldPerCatch = Math.Round(grossCatch, 2),
                    GrossGoldPerAttempt = grossAttempt,
                    DurabilityUpkeepPerAttempt = upkeep,
                    AccidentSinkPerAttempt = accidentSink,
                    ConsumableSinkPerAttempt = consumableSink,
                    NetGoldPerAttempt = netAttempt,
                    ProfitMarginPercent = margin,
                    EquippedItems = items.Select(i => i.Name).ToList()
                });
            }

            return model;
        }

        public async Task<BalanceSimulationResult> SimulateScenario(BalanceSimulationScenario scenario)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fishTypes = await context.FishTypes.AsNoTracking().Include(f => f.Categories).Where(f => f.Enabled).ToListAsync();
            var shopItems = await context.FishingShopItems.AsNoTracking().ToListAsync();

            var lineSnap = Math.Clamp(scenario.LineSnapChance, 0.0, 1.0);
            var rodSnap = Math.Clamp(scenario.RodSnapChance, 0.0, 1.0);
            var reelJam = Math.Clamp(scenario.ReelJamChance, 0.0, 1.0);
            var tackleLost = Math.Clamp(scenario.TackleBoxLostChance, 0.0, 1.0);
            var netBreak = Math.Clamp(scenario.NetBreakChance, 0.0, 1.0);
            var repairMultiplier = Math.Max(0.0, scenario.RepairCostMultiplier);

            var successRate = (1.0 - rodSnap) * (1.0 - lineSnap) * (1.0 - reelJam) * (1.0 - tackleLost) * (1.0 - netBreak);
            var combinedAccidentRate = 1.0 - successRate;

            List<FishingShopItem> equippedItems;
            if (string.Equals(scenario.SelectedLoadoutTier, "Custom", StringComparison.OrdinalIgnoreCase))
            {
                equippedItems = shopItems.Where(i => scenario.CustomShopItemIds.Contains(i.Id)).ToList();
            }
            else
            {
                equippedItems = ResolveTierItems(scenario.SelectedLoadoutTier, shopItems);
            }

            var rarityBoost = 0.0;
            var starBoost = 0.0;
            var weightBoost = 0.0;

            foreach (var item in equippedItems)
            {
                AccumulateBoosts(item, ref rarityBoost, ref starBoost, ref weightBoost);
            }

            var grossGoldPerCatch = fishTypes.Any()
                ? CalculateExpectedGoldWithBoosts(fishTypes, rarityBoost, starBoost, weightBoost)
                : 0.0;

            var grossGoldPerAttempt = grossGoldPerCatch * successRate;
            var durabilityUpkeep = CalculateExpectedDurabilityUpkeep(equippedItems, repairMultiplier);
            var accidentSink = CalculateExpectedAccidentSink(equippedItems, lineSnap, rodSnap, reelJam, tackleLost, netBreak);
            var consumableSink = CalculateExpectedConsumableSink(equippedItems);

            var netGoldPerAttempt = Math.Round(grossGoldPerAttempt - durabilityUpkeep - accidentSink - consumableSink, 2);
            var attemptsPerSession = Math.Max(1.0, scenario.AttemptsPerSession);
            var streamsPerWeek = Math.Max(0.1, scenario.StreamsPerWeek);
            var netGoldPerSession = Math.Round(netGoldPerAttempt * attemptsPerSession, 2);
            var netGoldPerWeek = Math.Round(netGoldPerSession * streamsPerWeek, 2);

            var margin = grossGoldPerAttempt > 0
                ? Math.Round((netGoldPerAttempt / grossGoldPerAttempt) * 100.0, 1)
                : 0.0;

            var topGearItems = ResolveTierItems("Top", shopItems);
            var topGearCost = topGearItems.Sum(i => i.Cost);

            var sessionsToAfford = (netGoldPerSession > 0 && topGearCost > 0)
                ? Math.Round(topGearCost / netGoldPerSession, 1)
                : 999.0;
            var weeksToAfford = (netGoldPerWeek > 0 && topGearCost > 0)
                ? Math.Round(topGearCost / netGoldPerWeek, 1)
                : 999.0;

            string financialStatus;
            if (netGoldPerAttempt <= 0)
                financialStatus = "Deflationary Loss (Players lose gold)";
            else if (margin < 25.0)
                financialStatus = "Tight Margin (High upkeep / risk)";
            else if (margin <= 75.0)
                financialStatus = "Sustainable Growth (Balanced progression)";
            else
                financialStatus = "Rapid Accumulation (Very generous)";

            return new BalanceSimulationResult
            {
                SuccessRatePercent = Math.Round(successRate * 100.0, 2),
                CombinedAccidentRatePercent = Math.Round(combinedAccidentRate * 100.0, 2),
                GrossGoldPerCatch = Math.Round(grossGoldPerCatch, 2),
                GrossGoldPerAttempt = Math.Round(grossGoldPerAttempt, 2),
                DurabilityUpkeepPerAttempt = Math.Round(durabilityUpkeep, 2),
                AccidentSinkPerAttempt = Math.Round(accidentSink, 2),
                ConsumableSinkPerAttempt = Math.Round(consumableSink, 2),
                NetGoldPerAttempt = netGoldPerAttempt,
                NetGoldPerSession = netGoldPerSession,
                NetGoldPerWeek = netGoldPerWeek,
                ProfitMarginPercent = margin,
                TotalLoadoutCost = equippedItems.Sum(i => i.Cost),
                SessionsToAffordTopGear = sessionsToAfford,
                WeeksToAffordTopGear = weeksToAfford,
                FinancialStatus = financialStatus,
                EquippedItemNames = equippedItems.Select(i => i.Name).ToList()
            };
        }

        public async Task<List<ItemEconomyAnalysis>> CalculateItemEconomyAnalysis()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var shopItems = await context.FishingShopItems
                .AsNoTracking()
                .Include(i => i.TargetFishType)
                .Where(i => i.Enabled)
                .ToListAsync();

            var fishTypes = await context.FishTypes.AsNoTracking().Include(f => f.Categories).Where(f => f.Enabled).ToListAsync();
            var settings = await _fishingService.GetSettings() ?? new FishingSettings();

            var lineSnap = Math.Clamp(settings.LineSnapChance, 0.0, 1.0);
            var rodSnap = Math.Clamp(settings.RodSnapChance, 0.0, 1.0);
            var reelJam = Math.Clamp(settings.ReelJamChance, 0.0, 1.0);
            var tackleLost = Math.Clamp(settings.TackleBoxLostChance, 0.0, 1.0);
            var netBreak = Math.Clamp(settings.NetBreakChance, 0.0, 1.0);
            var repairMultiplier = Math.Max(0.0, settings.RepairCostMultiplier);

            var successRate = (1.0 - rodSnap) * (1.0 - lineSnap) * (1.0 - reelJam) * (1.0 - tackleLost) * (1.0 - netBreak);
            var baselineGrossGoldPerCatch = await CalculateBaselineExpectedGold();
            var baselineGrossGoldPerAttempt = baselineGrossGoldPerCatch * successRate;

            var userGolds = await context.FishingGolds.AsNoTracking().Select(g => g.TotalGold).ToListAsync();
            var totalUsers = userGolds.Count;

            var analysisList = new List<ItemEconomyAnalysis>();

            foreach (var item in shopItems)
            {
                var hasDurability = item.MaxDurability.HasValue && item.MaxDurability.Value > 0;
                var isUnbreakable = !hasDurability && !item.IsConsumable && item.DisableBreaking;

                // Durability Upkeep
                double durabilityUpkeep = 0.0;
                if (hasDurability)
                {
                    var loss = Math.Max(0.0, item.DurabilityLossPerUse ?? 1.0);
                    var wearRatio = loss / item.MaxDurability!.Value;
                    var effectiveMult = repairMultiplier > 0 ? repairMultiplier : 1.0;
                    durabilityUpkeep = item.Cost * wearRatio * effectiveMult;
                }

                // Consumable per use cost
                double consumableCost = 0.0;
                if (item.IsConsumable && item.MaxUses.HasValue && item.MaxUses.Value > 0)
                {
                    consumableCost = (double)item.Cost / item.MaxUses.Value;
                }

                // Accident Risk
                double accidentRisk = 0.0;
                if (!item.DisableBreaking)
                {
                    var perUseCostForAccident = item.IsConsumable && item.MaxUses.HasValue && item.MaxUses.Value > 0
                        ? (double)item.Cost / item.MaxUses.Value
                        : item.Cost;

                    accidentRisk = item.EquipmentSlot switch
                    {
                        EquipmentSlot.Rod => rodSnap * item.Cost,
                        EquipmentSlot.Line or EquipmentSlot.Hook => (rodSnap + ((1.0 - rodSnap) * lineSnap)) * item.Cost,
                        EquipmentSlot.Reel => reelJam * item.Cost,
                        EquipmentSlot.TackleBox => tackleLost * item.Cost,
                        EquipmentSlot.Net => netBreak * item.Cost,
                        EquipmentSlot.Bait or EquipmentSlot.Lure => (rodSnap + ((1.0 - rodSnap) * lineSnap)) * perUseCostForAccident,
                        _ => 0.0
                    };
                }

                var totalOperatingCost = durabilityUpkeep + accidentRisk + consumableCost;

                // Expected Gross Boost
                var rarityBoost = 0.0;
                var starBoost = 0.0;
                var weightBoost = 0.0;
                AccumulateBoosts(item, ref rarityBoost, ref starBoost, ref weightBoost);

                var grossCatchWithItem = fishTypes.Any()
                    ? CalculateExpectedGoldWithBoosts(fishTypes, rarityBoost, starBoost, weightBoost)
                    : baselineGrossGoldPerCatch;
                var grossAttemptWithItem = grossCatchWithItem * successRate;
                var grossBoost = Math.Max(0.0, grossAttemptWithItem - baselineGrossGoldPerAttempt);

                var netValue = grossBoost - totalOperatingCost;
                double? paybackAttempts = null;
                if (netValue > 0 && item.Cost > 0)
                {
                    paybackAttempts = Math.Round(item.Cost / netValue, 0);
                }

                // Rating
                string rating;
                if (item.IsConsumable)
                {
                    var ratio = consumableCost > 0 && baselineGrossGoldPerCatch > 0
                        ? consumableCost / baselineGrossGoldPerCatch
                        : 1.0;
                    rating = ratio switch
                    {
                        <= 0.5 => "Great Value",
                        <= 1.0 => "Good Value",
                        <= 2.0 => "Fair Trade",
                        _ => "Consumable Sink"
                    };
                }
                else
                {
                    if (netValue > 0 && paybackAttempts.HasValue && paybackAttempts.Value <= 500)
                        rating = "Profitable Investment";
                    else if (netValue > 0)
                        rating = "Fair Upgrade";
                    else
                        rating = "Luxury Sink";
                }

                // Affordability
                var affordCount = userGolds.Count(g => g >= item.Cost);
                var affordPct = totalUsers > 0 ? Math.Round((double)affordCount / totalUsers * 100.0, 1) : 0;
                var effGoldPerAttempt = baselineGrossGoldPerAttempt > 0 ? baselineGrossGoldPerAttempt : 10.0;
                var attemptsNeeded = Math.Round(item.Cost / effGoldPerAttempt, 0);

                var analysis = new ItemEconomyAnalysis
                {
                    ShopItemId = item.Id,
                    ItemName = item.Name,
                    EquipmentSlot = item.EquipmentSlot?.ToString() ?? "None",
                    Cost = item.Cost,
                    IsConsumable = item.IsConsumable,
                    MaxUses = item.MaxUses,
                    MaxDurability = item.MaxDurability,
                    DurabilityLossPerUse = item.DurabilityLossPerUse ?? 1.0,
                    DisableBreaking = item.DisableBreaking,
                    IsUnbreakable = isUnbreakable,
                    DurabilityUpkeepPerAttempt = Math.Round(durabilityUpkeep, 2),
                    AccidentRiskPerAttempt = Math.Round(accidentRisk, 2),
                    TotalOperatingCostPerAttempt = Math.Round(totalOperatingCost, 2),
                    ExpectedGrossGoldBoostPerAttempt = Math.Round(grossBoost, 2),
                    NetValuePerAttempt = Math.Round(netValue, 2),
                    PaybackAttempts = paybackAttempts,
                    EconomicRating = rating,
                    PlayersWhoCanAfford = affordCount,
                    PercentageWhoCanAfford = affordPct,
                    AttemptsNeededToBuy = attemptsNeeded,
                    SessionsToAffordCasual = Math.Round(attemptsNeeded / 15.0, 1),
                    SessionsToAffordActive = Math.Round(attemptsNeeded / 30.0, 1),
                    SessionsToAffordHardcore = Math.Round(attemptsNeeded / 100.0, 1),
                    WeeksToAffordActive = Math.Round((attemptsNeeded / 30.0) / 3.0, 1)
                };

                PopulateItemEffectPreview(analysis, item, fishTypes, settings);
                analysisList.Add(analysis);
            }

            return analysisList;
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
            var lineSnapChance = Math.Clamp(settings.LineSnapChance, 0.0, 1.0);
            var rodSnapChance = Math.Clamp(settings.RodSnapChance, 0.0, 1.0);
            var reelJamChance = Math.Clamp(settings.ReelJamChance, 0.0, 1.0);
            var tackleBoxLostChance = Math.Clamp(settings.TackleBoxLostChance, 0.0, 1.0);
            var netBreakChance = Math.Clamp(settings.NetBreakChance, 0.0, 1.0);
            var repairCostMultiplier = Math.Max(0.0, settings.RepairCostMultiplier);

            var successfulAttemptChance = (1.0 - rodSnapChance) * (1.0 - lineSnapChance) * (1.0 - reelJamChance) * (1.0 - tackleBoxLostChance) * (1.0 - netBreakChance);
            var combinedAccidentChance = 1.0 - successfulAttemptChance;

            report.SettingsSnapshot = new BalanceSettingsSnapshot
            {
                LineSnapChance = lineSnapChance,
                RodSnapChance = rodSnapChance,
                ReelJamChance = reelJamChance,
                TackleBoxLostChance = tackleBoxLostChance,
                NetBreakChance = netBreakChance,
                RepairCostMultiplier = repairCostMultiplier,
                BoostMode = settings.BoostMode,
                BoostModeRarityMultiplier = settings.BoostModeRarityMultiplier,
                TheoreticalSuccessRatePercent = Math.Round(successfulAttemptChance * 100.0, 2),
                CombinedAccidentRatePercent = Math.Round(combinedAccidentChance * 100.0, 2)
            };

            // Calculate Projected Economics
            report.ProjectedEconomy = await CalculateProjectedEconomy();

            // Calculate Item Economics & ROI
            report.ItemAnalysis = await CalculateItemEconomyAnalysis();

            var topGearItems = report.ProjectedEconomy.TierEconomics
                .FirstOrDefault(t => t.TierName == "Top");
            report.TopGearTotalCost = topGearItems?.TotalLoadoutCost ?? 0;

            // Query Catches in date range
            var catchesQuery = context.FishCatches.Include(c => c.FishType).AsQueryable();
            if (startDate.HasValue) catchesQuery = catchesQuery.Where(c => c.CaughtAt >= startDate.Value);
            if (endDate.HasValue) catchesQuery = catchesQuery.Where(c => c.CaughtAt <= endDate.Value);
            var catches = await catchesQuery.ToListAsync();

            // Query Snap Events in date range
            var snapQuery = context.FishingSnapEvents.AsQueryable();
            if (startDate.HasValue) snapQuery = snapQuery.Where(s => s.SnappedAt >= startDate.Value);
            if (endDate.HasValue) snapQuery = snapQuery.Where(s => s.SnappedAt <= endDate.Value);
            var snapEvents = await snapQuery.ToListAsync();

            // Combine into unified attempts
            var attemptSamples = catches
                .Select(c => new { c.UserId, Timestamp = c.CaughtAt, IsCatch = true })
                .Concat(snapEvents.Select(s => new { s.UserId, Timestamp = s.SnappedAt, IsCatch = false }))
                .OrderBy(a => a.Timestamp)
                .ToList();

            var observedAttempts = attemptSamples.Count;
            var hasObservedData = observedAttempts > 0;
            var hasSnapData = snapEvents.Count > 0;

            var real = new RealEconomyTelemetry
            {
                TotalCatches = catches.Count,
                UniqueFishers = attemptSamples.Any()
                    ? attemptSamples.Select(a => a.UserId).Distinct().Count()
                    : catches.Select(c => c.UserId).Distinct().Count(),
                TotalAttemptsRecorded = hasSnapData ? observedAttempts : (successfulAttemptChance > 0 ? (int)Math.Round(catches.Count / successfulAttemptChance) : catches.Count),
                AttemptDataSource = hasSnapData
                    ? "Observed telemetry (catches + recorded accidents)"
                    : "Estimated from catch rate model (no accident telemetry in range)"
            };

            // Accident breakdown
            real.TotalAccidentsRecorded = snapEvents.Count;
            real.RodSnapsRecorded = snapEvents.Count(s => string.Equals(s.SnapType, "Rod", StringComparison.OrdinalIgnoreCase));
            real.LineSnapsRecorded = snapEvents.Count(s => string.Equals(s.SnapType, "Line", StringComparison.OrdinalIgnoreCase));
            real.ReelJamsRecorded = snapEvents.Count(s => string.Equals(s.SnapType, "Reel", StringComparison.OrdinalIgnoreCase));
            real.TackleBoxLossesRecorded = snapEvents.Count(s => string.Equals(s.SnapType, "TackleBox", StringComparison.OrdinalIgnoreCase));
            real.NetBreaksRecorded = snapEvents.Count(s => string.Equals(s.SnapType, "Net", StringComparison.OrdinalIgnoreCase));
            real.TotalGoldLostToAccidents = snapEvents.Sum(s => s.TotalGoldLost);

            if (real.TotalAttemptsRecorded > 0)
            {
                real.AverageAccidentLossPerAttempt = (double)real.TotalGoldLostToAccidents / real.TotalAttemptsRecorded;
                real.ObservedSuccessRatePercent = Math.Round((double)real.TotalCatches / real.TotalAttemptsRecorded * 100.0, 2);
            }
            else
            {
                real.ObservedSuccessRatePercent = Math.Round(successfulAttemptChance * 100.0, 2);
            }

            // Durability upkeep calculation from active users' equipped loadouts
            var activeUserIds = attemptSamples.Select(a => a.UserId).Distinct().ToList();
            if (!activeUserIds.Any() && catches.Any())
            {
                activeUserIds = catches.Select(c => c.UserId).Distinct().ToList();
            }

            double realDurabilityUpkeep = 0.0;
            var midTierUpkeep = report.ProjectedEconomy.TierEconomics.FirstOrDefault(t => t.TierName == "Mid")?.DurabilityUpkeepPerAttempt ?? 1.5;

            var userUpkeepMap = new Dictionary<string, double>();
            if (activeUserIds.Any() && real.TotalAttemptsRecorded > 0)
            {
                var activeUserEquippedBoosts = await context.UserFishingBoosts
                    .AsNoTracking()
                    .Include(b => b.ShopItem)
                    .Where(b => activeUserIds.Contains(b.UserId) && b.IsEquipped && b.ShopItem != null)
                    .ToListAsync();

                // Group by user and resolve equipped slot (in case of duplicate slot items, use most recent)
                var userEquippedGear = activeUserEquippedBoosts
                    .GroupBy(b => b.UserId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.GroupBy(b => b.ShopItem?.EquipmentSlot)
                              .Select(slotGroup => slotGroup.OrderByDescending(b => b.PurchasedAt).ThenByDescending(b => b.Id).First())
                              .ToList());

                // Group attempts by user
                var attemptsByUser = attemptSamples
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.Count());

                if (!attemptsByUser.Any() && catches.Any())
                {
                    attemptsByUser = catches
                        .GroupBy(c => c.UserId)
                        .ToDictionary(g => g.Key, g => g.Count());
                }

                double totalWeightedUpkeep = 0.0;
                int totalAttemptsCounted = 0;

                foreach (var (userId, userAttempts) in attemptsByUser)
                {
                    double userUpkeepPerAttempt = 0.0;
                    if (userEquippedGear.TryGetValue(userId, out var userBoosts))
                    {
                        foreach (var boost in userBoosts)
                        {
                            var shopItem = boost.ShopItem;
                            if (shopItem?.MaxDurability.HasValue == true && shopItem.MaxDurability.Value > 0)
                            {
                                var lossPerUse = Math.Max(0.0, shopItem.DurabilityLossPerUse ?? 1.0);
                                var wearRatio = lossPerUse / shopItem.MaxDurability.Value;
                                var effectiveMultiplier = repairCostMultiplier > 0 ? repairCostMultiplier : 1.0;
                                userUpkeepPerAttempt += shopItem.Cost * wearRatio * effectiveMultiplier;
                            }
                        }
                    }
                    userUpkeepMap[userId] = userUpkeepPerAttempt;
                    totalWeightedUpkeep += userUpkeepPerAttempt * userAttempts;
                    totalAttemptsCounted += userAttempts;
                }

                if (totalAttemptsCounted > 0)
                {
                    realDurabilityUpkeep = totalWeightedUpkeep / totalAttemptsCounted;
                }
            }

            // Fallback to mid-tier projection if no user gear was found or no telemetry in range
            if (realDurabilityUpkeep <= 0.0 && (!activeUserIds.Any() || real.TotalAttemptsRecorded == 0))
            {
                realDurabilityUpkeep = midTierUpkeep;
            }

            real.AverageDurabilityUpkeepPerAttempt = Math.Round(realDurabilityUpkeep, 2);
            real.EstimatedDurabilityUpkeepIncurred = Math.Round(realDurabilityUpkeep * real.TotalAttemptsRecorded, 2);

            // Gold Metrics
            if (catches.Any())
            {
                var goldValues = catches.Select(c => c.GoldEarned).OrderBy(g => g).ToList();
                real.TotalGrossGoldEarned = goldValues.Sum();
                real.AverageGoldPerCatch = Math.Round((double)real.TotalGrossGoldEarned / catches.Count, 2);
                real.MedianGoldPerCatch = goldValues.Count % 2 == 0
                    ? (goldValues[goldValues.Count / 2 - 1] + goldValues[goldValues.Count / 2]) / 2.0
                    : goldValues[goldValues.Count / 2];

                var grossGoldPerAttempt = (real.ObservedSuccessRatePercent / 100.0) * real.AverageGoldPerCatch;
                real.NetGoldPerAttempt = Math.Round(grossGoldPerAttempt - real.AverageAccidentLossPerAttempt - real.AverageDurabilityUpkeepPerAttempt, 2);
                real.NetTotalGoldFlow = Math.Round((double)real.TotalGrossGoldEarned - (double)real.TotalGoldLostToAccidents - real.EstimatedDurabilityUpkeepIncurred, 2);
            }
            else
            {
                real.NetGoldPerAttempt = Math.Round(report.ProjectedEconomy.BaselineGrossGoldPerAttempt - (report.ProjectedEconomy.TierEconomics.FirstOrDefault(t => t.TierName == "Mid")?.AccidentSinkPerAttempt ?? 0.0), 2);
                real.NetTotalGoldFlow = 0;
            }

            // Median Net Gold Per Attempt across all observed and estimated attempts
            var netAttemptValues = new List<double>();
            foreach (var c in catches)
            {
                var upkeep = userUpkeepMap.TryGetValue(c.UserId, out var u) ? u : real.AverageDurabilityUpkeepPerAttempt;
                netAttemptValues.Add(c.GoldEarned - upkeep);
            }
            foreach (var s in snapEvents)
            {
                var upkeep = userUpkeepMap.TryGetValue(s.UserId, out var u) ? u : real.AverageDurabilityUpkeepPerAttempt;
                netAttemptValues.Add(-(double)s.TotalGoldLost - upkeep);
            }

            var unobservedAttempts = Math.Max(0, real.TotalAttemptsRecorded - netAttemptValues.Count);
            for (int i = 0; i < unobservedAttempts; i++)
            {
                netAttemptValues.Add(-real.AverageDurabilityUpkeepPerAttempt);
            }

            if (netAttemptValues.Any())
            {
                netAttemptValues.Sort();
                var mid = netAttemptValues.Count / 2;
                real.MedianNetGoldPerAttempt = netAttemptValues.Count % 2 == 0
                    ? Math.Round((netAttemptValues[mid - 1] + netAttemptValues[mid]) / 2.0, 2)
                    : Math.Round(netAttemptValues[mid], 2);
            }
            else
            {
                real.MedianNetGoldPerAttempt = real.NetGoldPerAttempt;
            }

            // Engagement percentiles
            var userSessionAverages = CalculateUserAverageCatchesPerSession(
                attemptSamples.Select(a => (a.UserId, a.Timestamp)),
                SessionGap,
                minTotalCatches: 3,
                minSessions: 1);

            if (!userSessionAverages.Any())
            {
                userSessionAverages = CalculateUserAverageCatchesPerSession(
                    attemptSamples.Select(a => (a.UserId, a.Timestamp)),
                    SessionGap);
            }

            userSessionAverages = userSessionAverages.OrderBy(v => v).ToList();

            double casual = 15.0;
            double active = 30.0;
            double hardcore = 100.0;

            if (userSessionAverages.Count > 0)
            {
                var p25Index = Math.Max(0, (int)Math.Ceiling(userSessionAverages.Count * 0.25) - 1);
                casual = Math.Max(userSessionAverages[p25Index], 1.0);

                var p50Index = userSessionAverages.Count / 2;
                active = userSessionAverages.Count % 2 == 0
                    ? (userSessionAverages[p50Index - 1] + userSessionAverages[p50Index]) / 2.0
                    : userSessionAverages[p50Index];

                var p75Index = Math.Min(userSessionAverages.Count - 1, (int)Math.Ceiling(userSessionAverages.Count * 0.75) - 1);
                hardcore = userSessionAverages[p75Index];
            }

            var (streamsPerWeek, _, analysisWindowDays) = CalculateStreamsPerWeekFromAttempts(
                attemptSamples.Select(a => a.Timestamp),
                startDate,
                endDate);
            var analysisWeeks = Math.Max(analysisWindowDays / 7.0, 1.0 / 7.0);

            real.CasualAttemptsPerSession = Math.Round(casual, 1);
            real.ActiveAttemptsPerSession = Math.Round(active, 1);
            real.HardcoreAttemptsPerSession = Math.Round(hardcore, 1);
            real.StreamsPerWeek = streamsPerWeek > 0 ? streamsPerWeek : 3.0;
            real.AttemptsPerWeek = Math.Round(real.TotalAttemptsRecorded / analysisWeeks, 1);
            real.CatchesPerWeek = Math.Round(real.TotalCatches / analysisWeeks, 1);

            report.RealEconomy = real;

            // Variance Calculation
            if (catches.Any())
            {
                var baselineCatch = report.ProjectedEconomy.BaselineGrossGoldPerCatch;
                report.Variance = new EconomyVarianceSummary
                {
                    GrossGoldCatchVariancePercent = baselineCatch > 0 ? Math.Round(((real.AverageGoldPerCatch / baselineCatch) - 1.0) * 100.0, 1) : 0,
                    SuccessRateVariancePercent = Math.Round(real.ObservedSuccessRatePercent - report.SettingsSnapshot.TheoreticalSuccessRatePercent, 1),
                    AccidentRateVariancePercent = Math.Round((100.0 - real.ObservedSuccessRatePercent) - report.SettingsSnapshot.CombinedAccidentRatePercent, 1),
                    NetGoldAttemptVariancePercent = report.ProjectedEconomy.BaselineGrossGoldPerAttempt > 0
                        ? Math.Round(((real.NetGoldPerAttempt / report.ProjectedEconomy.BaselineGrossGoldPerAttempt) - 1.0) * 100.0, 1)
                        : 0,
                    SummaryNotes = real.ObservedSuccessRatePercent >= report.SettingsSnapshot.TheoreticalSuccessRatePercent - 2.0
                        ? "Real catch performance aligns closely with theoretical probability models."
                        : "Observed success rate is lower than expected; check accident logs and player equipment usage."
                };
            }

            // Progression Milestones
            var projectionWindows = new[]
            {
                (Weeks: 12, Label: "3 Months (12 weeks)"),
                (Weeks: 26, Label: "6 Months (26 weeks)"),
                (Weeks: 52, Label: "1 Year (52 weeks)")
            };

            var effGold = Math.Max(0.0, real.NetGoldPerAttempt);
            var effCatchRate = real.ObservedSuccessRatePercent / 100.0;

            foreach (var window in projectionWindows)
            {
                var milestone = new LoadoutProgressionMilestone
                {
                    Weeks = window.Weeks,
                    Label = window.Label
                };

                foreach (var tierProfile in new[]
                {
                    (Name: "Casual", Attempts: real.CasualAttemptsPerSession),
                    (Name: "Active", Attempts: real.ActiveAttemptsPerSession),
                    (Name: "Hardcore", Attempts: real.HardcoreAttemptsPerSession)
                })
                {
                    var attemptsPerWeek = tierProfile.Attempts * real.StreamsPerWeek;
                    var projectedAttempts = attemptsPerWeek * window.Weeks;
                    var projectedCatches = projectedAttempts * effCatchRate;
                    var projectedGrossGold = projectedCatches * (real.AverageGoldPerCatch > 0 ? real.AverageGoldPerCatch : report.ProjectedEconomy.BaselineGrossGoldPerCatch);
                    var projectedAccidentSink = projectedAttempts * real.AverageAccidentLossPerAttempt;
                    var projectedUpkeepSink = projectedAttempts * real.AverageDurabilityUpkeepPerAttempt;
                    var projectedNetGold = Math.Max(0, projectedGrossGold - projectedAccidentSink - projectedUpkeepSink);
                    var maxGearProgress = report.TopGearTotalCost > 0
                        ? Math.Min(999.0, (projectedNetGold / report.TopGearTotalCost) * 100.0)
                        : 0;

                    milestone.Tiers.Add(new LoadoutProgressionTier
                    {
                        TierName = tierProfile.Name,
                        AttemptsPerSession = Math.Round(tierProfile.Attempts, 1),
                        StreamsPerWeek = real.StreamsPerWeek,
                        AttemptsPerWeek = Math.Round(attemptsPerWeek, 1),
                        ProjectedAttempts = Math.Round(projectedAttempts, 0),
                        ProjectedCatches = Math.Round(projectedCatches, 0),
                        ProjectedGrossGold = Math.Round(projectedGrossGold, 0),
                        ProjectedAccidentSink = Math.Round(projectedAccidentSink, 0),
                        ProjectedUpkeepSink = Math.Round(projectedUpkeepSink, 0),
                        ProjectedNetGold = Math.Round(projectedNetGold, 0),
                        MaxGearProgressPercent = Math.Round(maxGearProgress, 1)
                    });
                }

                report.ProgressionMilestones.Add(milestone);
            }

            // Automated Diagnostics & Health Checks
            EvaluateHealthDiagnostics(report);

            return report;
        }

        private static void EvaluateHealthDiagnostics(FishingBalanceReport report)
        {
            var diagnostics = new List<BalanceHealthDiagnostic>();
            var recommendations = new List<string>();

            // 1. Deflation check
            if (report.RealEconomy.NetGoldPerAttempt <= 0)
            {
                var diag = new BalanceHealthDiagnostic
                {
                    Severity = DiagnosticSeverity.Error,
                    Title = "Deflationary Death Spiral Detected",
                    Message = $"Net gold per attempt is {report.RealEconomy.NetGoldPerAttempt:F2}g. On average, players lose gold by fishing.",
                    RemediationAdvice = "Decrease accident failure chances (Line/Rod snaps, Reel jams) or lower repair cost multipliers."
                };
                diagnostics.Add(diag);
                recommendations.Add($"🚨 {diag.Title}: {diag.Message} {diag.RemediationAdvice}");
            }

            // 2. High accident failure rate
            if (report.SettingsSnapshot.CombinedAccidentRatePercent > 7.0)
            {
                var diag = new BalanceHealthDiagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Title = "High Failure Rate",
                    Message = $"Combined accident probability is {report.SettingsSnapshot.CombinedAccidentRatePercent:F1}%. More than 1 in 14 casts result in lost gear.",
                    RemediationAdvice = "Consider lowering line snap (currently {report.SettingsSnapshot.LineSnapChance:P1}) or reel jam chance to reduce player frustration."
                };
                diagnostics.Add(diag);
                recommendations.Add($"⚠️ {diag.Title}: {diag.Message}");
            }

            // 3. Disabled accident chances
            var zeroChances = new List<string>();
            if (report.SettingsSnapshot.ReelJamChance <= 0) zeroChances.Add("Reel Jam");
            if (report.SettingsSnapshot.TackleBoxLostChance <= 0) zeroChances.Add("Tackle Box Lost");
            if (report.SettingsSnapshot.NetBreakChance <= 0) zeroChances.Add("Net Break");

            if (zeroChances.Any())
            {
                var diag = new BalanceHealthDiagnostic
                {
                    Severity = DiagnosticSeverity.Info,
                    Title = "Disabled Failure Types",
                    Message = $"The following accident types have 0% chance: {string.Join(", ", zeroChances)}.",
                    RemediationAdvice = "If you want a full equipment economy sink, configure non-zero values (e.g. 0.5% - 1.0%) in Fishing Settings."
                };
                diagnostics.Add(diag);
                recommendations.Add($"ℹ️ {diag.Title}: {diag.Message}");
            }

            // 4. Repair cost multiplier check
            if (report.SettingsSnapshot.RepairCostMultiplier <= 0)
            {
                var diag = new BalanceHealthDiagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Title = "Repairs Disabled / Free",
                    Message = "RepairCostMultiplier is 0.0. Broken items cannot be repaired and must be repurchased, or repairs are completely free.",
                    RemediationAdvice = "Set RepairCostMultiplier between 0.15 and 0.50 for a healthy durability economy."
                };
                diagnostics.Add(diag);
                recommendations.Add($"⚠️ {diag.Title}: {diag.Message}");
            }
            else if (report.SettingsSnapshot.RepairCostMultiplier > 0.80)
            {
                var diag = new BalanceHealthDiagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Title = "High Repair Cost Multiplier",
                    Message = $"RepairCostMultiplier is set to {report.SettingsSnapshot.RepairCostMultiplier:F2}x. Upkeep costs will consume a large share of player earnings.",
                    RemediationAdvice = "Consider reducing to 0.25 - 0.40x to prevent high maintenance fees from stalling progression."
                };
                diagnostics.Add(diag);
                recommendations.Add($"⚠️ {diag.Title}: {diag.Message}");
            }

            // 5. Progression pacing check
            var overPriced = report.ItemAnalysis
                .Where(i => !i.IsConsumable && i.WeeksToAffordActive > 20)
                .ToList();
            if (overPriced.Any())
            {
                var diag = new BalanceHealthDiagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Title = "Very Slow Progression",
                    Message = $"{overPriced.Count} permanent items take more than 20 weeks for active players to afford.",
                    RemediationAdvice = "Consider lowering prices on top-tier items or increasing fish sell gold."
                };
                diagnostics.Add(diag);
                recommendations.Add($"⚠️ {diag.Title}: {diag.Message}");
            }

            // 6. Healthy status
            if (!diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning))
            {
                var diag = new BalanceHealthDiagnostic
                {
                    Severity = DiagnosticSeverity.Success,
                    Title = "Economy Health Excellent",
                    Message = "All failure rates, repair multipliers, and item ROI ratings are within optimal game balance targets.",
                    RemediationAdvice = "No changes required. Progression curve is smooth and rewarding."
                };
                diagnostics.Add(diag);
                recommendations.Add($"✅ {diag.Title}: {diag.Message}");
            }

            report.Diagnostics = diagnostics;
            report.BalanceRecommendations = recommendations;
        }

        public async Task<Dictionary<string, int>> CalculateRecommendedPricing(int targetWeeksForEndgame = 26)
        {
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

            userSessionAverages = userSessionAverages.OrderBy(v => v).ToList();

            double activeAttemptsPerSession = 30.0;
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
                streamsPerWeek = 3.0;
            }

            var expectedNetGoldPerAttempt = await CalculateProgressiveBaselineGold(targetWeeksForEndgame);
            if (expectedNetGoldPerAttempt <= 0)
            {
                expectedNetGoldPerAttempt = Math.Max(1.0, await CalculateBaselineExpectedGold() * 0.75);
            }

            var scaleFactor = targetWeeksForEndgame / 26.0;

            var pricingTiers = new List<(string Name, int TargetWeeks)>
            {
                ("Entry", Math.Max(1, (int)Math.Round(2 * scaleFactor))),
                ("Mid", Math.Max(2, (int)Math.Round(6 * scaleFactor))),
                ("High", Math.Max(4, (int)Math.Round(12 * scaleFactor))),
                ("Top", targetWeeksForEndgame)
            };

            var recommendations = new Dictionary<string, int>();

            var itemTiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Bamboo Rod", "Entry" },
                { "Basic Reel", "Entry" },
                { "Monofilament Line", "Entry" },
                { "Standard Hook", "Entry" },
                { "Basic Tackle Box", "Entry" },
                { "Landing Net", "Entry" },

                { "Fiberglass Rod", "Mid" },
                { "Precision Reel", "Mid" },
                { "Braided Line", "Mid" },
                { "Circle Hook", "Mid" },
                { "Pro Tackle Box", "Mid" },
                { "Knotless Net", "Mid" },

                { "Carbon Fiber Rod", "High" },
                { "Professional Reel", "High" },
                { "Fluorocarbon Line", "High" },
                { "Treble Hook", "High" },
                { "Master Tackle Box", "High" },
                { "Tournament Net", "High" },

                { "Legendary Rod", "Top" },
                { "Master Reel", "Top" },
                { "Titanium Wire", "Top" },
                { "Diamond Hook", "Top" }
            };

            foreach (var (itemName, tierName) in itemTiers)
            {
                var tier = pricingTiers.FirstOrDefault(t => t.Name == tierName);
                if (tier == default) continue;

                var sessionsNeeded = tier.TargetWeeks * streamsPerWeek;
                var attemptsNeeded = sessionsNeeded * activeAttemptsPerSession;
                var targetPrice = (int)Math.Round(attemptsNeeded * expectedNetGoldPerAttempt);

                recommendations[itemName] = Math.Max(10, targetPrice);
            }

            return recommendations;
        }

        #region Helper Calculation Methods
        private static void AccumulateBoosts(FishingShopItem item, ref double rarityBoost, ref double starBoost, ref double weightBoost)
        {
            ApplyBoostAmount(item.BoostType, item.BoostAmount, ref rarityBoost, ref starBoost, ref weightBoost);
            ApplyBoostAmount(item.BoostType2, item.BoostAmount2 ?? 0, ref rarityBoost, ref starBoost, ref weightBoost);
            ApplyBoostAmount(item.BoostType3, item.BoostAmount3 ?? 0, ref rarityBoost, ref starBoost, ref weightBoost);
        }

        private static void ApplyBoostAmount(FishingBoostType? boostType, double amount, ref double rarityBoost, ref double starBoost, ref double weightBoost)
        {
            if (boostType == null || amount <= 0) return;

            switch (boostType.Value)
            {
                case FishingBoostType.GeneralRarityBoost:
                    rarityBoost += amount;
                    break;
                case FishingBoostType.StarBoost:
                    starBoost += amount;
                    break;
                case FishingBoostType.WeightBoost:
                    weightBoost += amount;
                    break;
            }
        }

        private static List<FishingShopItem> ResolveTierItems(string tierName, List<FishingShopItem> allShopItems)
        {
            var byName = allShopItems.ToDictionary(i => i.Name, i => i, StringComparer.OrdinalIgnoreCase);
            var result = new List<FishingShopItem>();

            var targetNames = tierName switch
            {
                "Entry" => new[] { "Bamboo Rod", "Basic Reel", "Monofilament Line", "Standard Hook", "Basic Tackle Box", "Landing Net" },
                "Mid" => new[] { "Fiberglass Rod", "Precision Reel", "Braided Line", "Circle Hook", "Pro Tackle Box", "Knotless Net" },
                "High" => new[] { "Carbon Fiber Rod", "Professional Reel", "Fluorocarbon Line", "Treble Hook", "Master Tackle Box", "Tournament Net" },
                "Top" => new[] { "Legendary Rod", "Master Reel", "Titanium Wire", "Diamond Hook", "Master Tackle Box", "Tournament Net" },
                _ => Array.Empty<string>()
            };

            foreach (var name in targetNames)
            {
                if (byName.TryGetValue(name, out var item))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private static double CalculateExpectedAccidentSink(
            IEnumerable<FishingShopItem> items,
            double lineSnapChance,
            double rodSnapChance,
            double reelJamChance,
            double tackleBoxLostChance,
            double netBreakChance)
        {
            var list = items.Where(i => !i.DisableBreaking).ToList();
            var rodCost = list.FirstOrDefault(i => i.EquipmentSlot == EquipmentSlot.Rod)?.Cost ?? 0;
            var lineCost = list.FirstOrDefault(i => i.EquipmentSlot == EquipmentSlot.Line)?.Cost ?? 0;
            var hookCost = list.FirstOrDefault(i => i.EquipmentSlot == EquipmentSlot.Hook)?.Cost ?? 0;
            var reelCost = list.FirstOrDefault(i => i.EquipmentSlot == EquipmentSlot.Reel)?.Cost ?? 0;
            var tackleCost = list.FirstOrDefault(i => i.EquipmentSlot == EquipmentSlot.TackleBox)?.Cost ?? 0;
            var netCost = list.FirstOrDefault(i => i.EquipmentSlot == EquipmentSlot.Net)?.Cost ?? 0;

            var baitLureCost = 0.0;
            foreach (var baitLure in list.Where(i => i.EquipmentSlot == EquipmentSlot.Bait || i.EquipmentSlot == EquipmentSlot.Lure))
            {
                if (baitLure.MaxUses.HasValue && baitLure.MaxUses.Value > 0)
                    baitLureCost += (double)baitLure.Cost / baitLure.MaxUses.Value;
                else
                    baitLureCost += baitLure.Cost;
            }

            var rodLoss = rodCost + lineCost + hookCost + baitLureCost;
            var lineLoss = lineCost + hookCost + baitLureCost;
            var reelLoss = reelCost;
            var tackleLoss = tackleCost;
            var netLoss = netCost;

            return (rodSnapChance * rodLoss)
                 + ((1.0 - rodSnapChance) * lineSnapChance * lineLoss)
                 + (reelJamChance * reelLoss)
                 + (tackleBoxLostChance * tackleLoss)
                 + (netBreakChance * netLoss);
        }

        private static double CalculateExpectedDurabilityUpkeep(
            IEnumerable<FishingShopItem> items,
            double repairCostMultiplier)
        {
            double totalUpkeep = 0.0;
            foreach (var item in items)
            {
                if (item.MaxDurability.HasValue && item.MaxDurability.Value > 0)
                {
                    var lossPerUse = Math.Max(0.0, item.DurabilityLossPerUse ?? 1.0);
                    var wearRatio = lossPerUse / item.MaxDurability.Value;
                    var effectiveMultiplier = repairCostMultiplier > 0 ? repairCostMultiplier : 1.0;
                    totalUpkeep += item.Cost * wearRatio * effectiveMultiplier;
                }
            }
            return totalUpkeep;
        }

        private static double CalculateExpectedConsumableSink(IEnumerable<FishingShopItem> items)
        {
            double consumableSink = 0.0;
            foreach (var item in items)
            {
                if (item.IsConsumable && item.MaxUses.HasValue && item.MaxUses.Value > 0)
                {
                    consumableSink += (double)item.Cost / item.MaxUses.Value;
                }
            }
            return consumableSink;
        }

        private static List<ProgressionTier> BuildProgressionTiers(int targetWeeks)
        {
            var tiers = new List<ProgressionTier>
            {
                new() { Name = "Naked", Weeks = 2, RarityBoost = 0.0, StarBoost = 0.0, WeightBoost = 0.0 },
                new() { Name = "Entry", Weeks = 5, RarityBoost = 0.10, StarBoost = 0.10, WeightBoost = 0.30, RodName = "Bamboo Rod", LineName = "Monofilament Line", HookName = "Standard Hook" },
                new() { Name = "Mid", Weeks = 6, RarityBoost = 0.20, StarBoost = 0.20, WeightBoost = 0.55, RodName = "Fiberglass Rod", LineName = "Braided Line", HookName = "Circle Hook" },
                new() { Name = "High", Weeks = 7, RarityBoost = 0.30, StarBoost = 0.30, WeightBoost = 0.80, RodName = "Carbon Fiber Rod", LineName = "Fluorocarbon Line", HookName = "Treble Hook" },
                new() { Name = "Top", Weeks = 6, RarityBoost = 0.40, StarBoost = 0.42, WeightBoost = 0.95, RodName = "Legendary Rod", LineName = "Titanium Wire", HookName = "Diamond Hook" }
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

        private double CalculateExpectedGoldWithBoosts(
            List<FishType> fishTypes,
            double rarityBoost,
            double starBoost,
            double weightBoost)
        {
            var rarityWeights = FishingRarityWeightProfiles.CreateAvailableWeights(fishTypes);

            if (rarityBoost > 0)
            {
                foreach (var rarity in rarityWeights.Keys.ToList())
                {
                    if (rarity != FishRarity.Common)
                    {
                        rarityWeights[rarity] *= (1.0 + rarityBoost);
                    }
                }
            }

            var totalRarityWeight = rarityWeights.Values.Sum();
            if (totalRarityWeight <= 0) return 0.0;

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

        private Dictionary<FishRarity, double> CalculateRarityWeights(
            List<FishType> fishTypes,
            bool useBoostMode,
            double boostModeMultiplier,
            List<UserFishingBoost> mockBoosts)
        {
            var rarityWeights = FishingRarityWeightProfiles.CreateAvailableWeights(fishTypes);

            if (useBoostMode)
            {
                FishingRarityWeightProfiles.ApplyGlobalRarityMultiplier(rarityWeights, boostModeMultiplier);
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
            ApplySingleBoostToRarityWeights(rarityWeights, fishTypes, boost.ShopItem?.BoostType, boost.ShopItem?.BoostAmount ?? 0, boost.ShopItem?.TargetFishTypeId);
            ApplySingleBoostToRarityWeights(rarityWeights, fishTypes, boost.ShopItem?.BoostType2, boost.ShopItem?.BoostAmount2 ?? 0, boost.ShopItem?.TargetFishTypeId);
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
        }

        private double CalculateWithinRarityChance(FishType targetFish, List<FishType> fishOfRarity, List<UserFishingBoost> mockBoosts)
        {
            var targetedBoosts = mockBoosts.Where(b =>
                (b.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost ||
                 b.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost ||
                 b.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost) &&
                b.ShopItem.TargetFishTypeId != null ||
                (b.ShopItem?.BoostType == FishingBoostType.SpecificCategoryBoost ||
                 b.ShopItem?.BoostType2 == FishingBoostType.SpecificCategoryBoost ||
                 b.ShopItem?.BoostType3 == FishingBoostType.SpecificCategoryBoost) &&
                !string.IsNullOrWhiteSpace(b.ShopItem?.TargetCategory)).ToList();

            if (targetedBoosts.Any())
            {
                var weightedFish = new List<(FishType fish, double weight)>();
                foreach (var f in fishOfRarity)
                {
                    var weight = 1.0;
                    foreach (var boost in targetedBoosts)
                    {
                        weight *= FishingCalculations.GetTargetedBoostMultiplier(boost.ShopItem, f);
                    }
                    weightedFish.Add((f, weight));
                }

                var totalFishWeight = weightedFish.Sum(w => w.weight);
                var fishWeight = weightedFish.First(w => w.fish.Id == targetFish.Id).weight;
                return fishWeight / totalFishWeight;
            }

            return 1.0 / fishOfRarity.Count;
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
            ItemEconomyAnalysis analysis,
            FishingShopItem item,
            List<FishType> fishTypes,
            FishingSettings settings)
        {
            var boostEntries = new List<(FishingBoostType? Type, double Amount)>
            {
                (item.BoostType, item.BoostAmount),
                (item.BoostType2, item.BoostAmount2 ?? 0),
                (item.BoostType3, item.BoostAmount3 ?? 0)
            };

            var specificBoost = boostEntries.Where(b => b.Type == FishingBoostType.SpecificFishBoost).Sum(b => b.Amount);
            var categoryBoost = boostEntries.Where(b => b.Type == FishingBoostType.SpecificCategoryBoost).Sum(b => b.Amount);
            var generalBoost = boostEntries.Where(b => b.Type == FishingBoostType.GeneralRarityBoost).Sum(b => b.Amount);
            var starBoost = boostEntries.Where(b => b.Type == FishingBoostType.StarBoost).Sum(b => b.Amount);
            var weightBoost = boostEntries.Where(b => b.Type == FishingBoostType.WeightBoost).Sum(b => b.Amount);

            var baselineProbabilities = BuildFishProbabilityMap(
                fishTypes,
                settings.BoostMode,
                settings.BoostModeRarityMultiplier,
                new List<UserFishingBoost>());

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
                settings.BoostMode,
                settings.BoostModeRarityMultiplier,
                new List<UserFishingBoost> { mockBoost });

            if (specificBoost > 0 && item.TargetFishTypeId.HasValue &&
                baselineProbabilities.TryGetValue(item.TargetFishTypeId.Value, out var baselineTarget) &&
                withItemProbabilities.TryGetValue(item.TargetFishTypeId.Value, out var boostedTarget))
            {
                analysis.HasEffectPreview = true;
                analysis.EffectMetric = $"{baselineTarget.FishName} catch chance";
                analysis.EffectBaselineValue = baselineTarget.OverallChance;
                analysis.EffectWithItemValue = boostedTarget.OverallChance;
            }
            else if (categoryBoost > 0 && !string.IsNullOrWhiteSpace(item.TargetCategory))
            {
                var categoryFishIds = fishTypes
                    .Where(f => f.Categories.Any(c => c.Category.Equals(item.TargetCategory, StringComparison.OrdinalIgnoreCase)))
                    .Select(f => f.Id)
                    .ToHashSet();

                var baselineCategoryChance = baselineProbabilities.Values
                    .Where(p => categoryFishIds.Contains(p.FishId))
                    .Sum(p => p.OverallChance);
                var withItemCategoryChance = withItemProbabilities.Values
                    .Where(p => categoryFishIds.Contains(p.FishId))
                    .Sum(p => p.OverallChance);

                analysis.HasEffectPreview = true;
                analysis.EffectMetric = $"{item.TargetCategory} catch chance";
                analysis.EffectBaselineValue = Math.Round(baselineCategoryChance, 4);
                analysis.EffectWithItemValue = Math.Round(withItemCategoryChance, 4);
            }
            else if (generalBoost > 0)
            {
                var baselineUncommonPlus = baselineProbabilities.Values
                    .Where(p => p.Rarity != FishRarity.Common)
                    .Sum(p => p.OverallChance);
                var withItemUncommonPlus = withItemProbabilities.Values
                    .Where(p => p.Rarity != FishRarity.Common)
                    .Sum(p => p.OverallChance);

                analysis.HasEffectPreview = true;
                analysis.EffectMetric = "Uncommon+ catch chance";
                analysis.EffectBaselineValue = Math.Round(baselineUncommonPlus, 4);
                analysis.EffectWithItemValue = Math.Round(withItemUncommonPlus, 4);
            }
            else if (starBoost > 0)
            {
                var baselineThreeStar = BuildStarProbabilities(0.0)[3] * 100.0;
                var boostedThreeStar = BuildStarProbabilities(starBoost)[3] * 100.0;

                analysis.HasEffectPreview = true;
                analysis.EffectMetric = "3-star catch chance";
                analysis.EffectBaselineValue = Math.Round(baselineThreeStar, 2);
                analysis.EffectWithItemValue = Math.Round(boostedThreeStar, 2);
            }
            else if (weightBoost > 0)
            {
                analysis.HasEffectPreview = true;
                analysis.EffectMetric = "Average weight multiplier";
                analysis.EffectBaselineValue = 100.0;
                analysis.EffectWithItemValue = Math.Round((1.0 + weightBoost) * 100.0, 2);
            }

            if (analysis.HasEffectPreview && analysis.EffectBaselineValue > 0)
            {
                analysis.EffectRelativeChangePercent = Math.Round(
                    ((analysis.EffectWithItemValue / analysis.EffectBaselineValue) - 1.0) * 100.0,
                    2);
            }
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
                if (!ordered.Any()) continue;

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
            var attempts = attemptTimestamps.OrderBy(t => t).ToList();
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
            var activeDays = attempts.Select(t => t.Date).Distinct().Count();

            var windowWeeks = analysisWindowDays / 7.0;
            if (windowWeeks <= 0)
            {
                return (0, activeDays, analysisWindowDays);
            }

            var streamsPerWeek = Math.Min(7.0, activeDays / windowWeeks);
            return (Math.Round(streamsPerWeek, 2), activeDays, analysisWindowDays);
        }

        private static double ApplySlotLoss(List<UserFishingBoost> boosts, EquipmentSlot slot)
        {
            var item = boosts.FirstOrDefault(b => b.ShopItem?.EquipmentSlot == slot);
            if (item?.ShopItem != null && !item.ShopItem.DisableBreaking)
            {
                return item.ShopItem.Cost;
            }
            return 0.0;
        }

        private static double ApplyLineSnapLosses(List<UserFishingBoost> boosts)
        {
            var replacementCost = 0.0;
            foreach (var item in boosts.Where(b => (b.ShopItem?.EquipmentSlot == EquipmentSlot.Line || b.ShopItem?.EquipmentSlot == EquipmentSlot.Hook) && b.ShopItem?.DisableBreaking != true))
            {
                replacementCost += item.ShopItem?.Cost ?? 0;
            }

            var baitLureItems = boosts
                .Where(b => (b.ShopItem?.EquipmentSlot == EquipmentSlot.Bait || b.ShopItem?.EquipmentSlot == EquipmentSlot.Lure) && b.ShopItem?.DisableBreaking != true)
                .ToList();

            foreach (var item in baitLureItems)
            {
                if (item.RemainingUses == -1)
                {
                    replacementCost += item.ShopItem?.Cost ?? 0;
                }
                else if (item.RemainingUses > 0)
                {
                    var maxUses = item.ShopItem?.MaxUses ?? 1;
                    var perUseCost = maxUses > 0 ? (item.ShopItem?.Cost ?? 0) / (double)maxUses : item.ShopItem?.Cost ?? 0;
                    replacementCost += perUseCost;
                }
            }

            return replacementCost;
        }

        private static double ApplyRodSnapLosses(List<UserFishingBoost> boosts)
        {
            var replacementCost = 0.0;
            foreach (var rod in boosts.Where(b => b.ShopItem?.EquipmentSlot == EquipmentSlot.Rod && b.ShopItem?.DisableBreaking != true))
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
                if (boost.RemainingUses == -1) continue;

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
        #endregion
    }
}
