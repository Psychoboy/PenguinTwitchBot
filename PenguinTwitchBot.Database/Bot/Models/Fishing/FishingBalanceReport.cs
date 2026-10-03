using System;
using System.Collections.Generic;

namespace PenguinTwitchBot.Database.Bot.Models.Fishing
{
    public class FishingBalanceReport
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        // Settings Snapshot
        public BalanceSettingsSnapshot SettingsSnapshot { get; set; } = new();

        // Theoretical / Projected Economics (Mathematical expectation from settings & items)
        public ProjectedEconomyModel ProjectedEconomy { get; set; } = new();

        // Real / Observed Telemetry (From DB catches, snap events, user golds)
        public RealEconomyTelemetry RealEconomy { get; set; } = new();

        // Side-by-side Variance Indicators (Real vs Projected)
        public EconomyVarianceSummary Variance { get; set; } = new();

        // Item Economics & ROI Deep-Dive
        public List<ItemEconomyAnalysis> ItemAnalysis { get; set; } = new();

        // Multi-month Loadout Progression Projections
        public List<LoadoutProgressionMilestone> ProgressionMilestones { get; set; } = new();

        // Automated Health Diagnostics & Remediation Flags
        public List<BalanceHealthDiagnostic> Diagnostics { get; set; } = new();

        #region Convenience Backwards-Compatible Properties
        public int TotalCatches => RealEconomy.TotalCatches;
        public int UniqueUsers => RealEconomy.UniqueFishers;
        public int TotalGoldEarned => (int)RealEconomy.TotalGrossGoldEarned;
        public double AverageGoldPerCatch => RealEconomy.AverageGoldPerCatch;
        public double MedianGoldPerCatch => RealEconomy.MedianGoldPerCatch;
        public double ConfiguredLineSnapChance => SettingsSnapshot.LineSnapChance;
        public double ConfiguredRodSnapChance => SettingsSnapshot.RodSnapChance;
        public double EstimatedSuccessfulAttemptRatePercent => RealEconomy.ObservedSuccessRatePercent > 0 
            ? RealEconomy.ObservedSuccessRatePercent 
            : SettingsSnapshot.TheoreticalSuccessRatePercent;
        public int EstimatedTotalAttempts => RealEconomy.TotalAttemptsRecorded;
        public int EstimatedFailedAttempts => Math.Max(0, RealEconomy.TotalAttemptsRecorded - RealEconomy.TotalCatches);
        public int EstimatedLineSnaps => RealEconomy.LineSnapsRecorded;
        public int EstimatedRodSnaps => RealEconomy.RodSnapsRecorded;
        public double EstimatedSnapReplacementCostPerAttempt => RealEconomy.AverageAccidentLossPerAttempt;
        public double EstimatedSnapReplacementCostTotal => (double)RealEconomy.TotalGoldLostToAccidents;
        public double SnapAdjustedAverageGoldPerAttempt => RealEconomy.NetGoldPerAttempt;
        public double SnapAdjustedMedianGoldPerAttempt => RealEconomy.MedianNetGoldPerAttempt;
        public double SnapAdjustedTotalNetGold => RealEconomy.NetTotalGoldFlow;
        public string AttemptDataSource => RealEconomy.AttemptDataSource;
        public double CasualAttemptsPerSession => RealEconomy.CasualAttemptsPerSession;
        public double ActiveAttemptsPerSession => RealEconomy.ActiveAttemptsPerSession;
        public double HardcoreAttemptsPerSession => RealEconomy.HardcoreAttemptsPerSession;
        public double StreamsPerWeekFromAttempts => RealEconomy.StreamsPerWeek;
        public double AttemptsPerWeek => RealEconomy.AttemptsPerWeek;
        public double CatchesPerWeek => RealEconomy.CatchesPerWeek;
        public int TopGearTotalCost { get; set; }
        public List<string> BalanceRecommendations { get; set; } = new();
        #endregion
    }

    public class BalanceSettingsSnapshot
    {
        public double LineSnapChance { get; set; }
        public double RodSnapChance { get; set; }
        public double ReelJamChance { get; set; }
        public double TackleBoxLostChance { get; set; }
        public double NetBreakChance { get; set; }
        public double RepairCostMultiplier { get; set; }
        public bool BoostMode { get; set; }
        public double BoostModeRarityMultiplier { get; set; }
        public double CombinedAccidentRatePercent { get; set; }
        public double TheoreticalSuccessRatePercent { get; set; }
    }

    public class ProjectedEconomyModel
    {
        public double BaselineGrossGoldPerCatch { get; set; }
        public double BaselineGrossGoldPerAttempt { get; set; }
        public double BaselineSuccessRatePercent { get; set; }

        // Progressive loadout tiers (Bare Hands, Entry, Mid, High, Top)
        public List<TierProjectedEconomics> TierEconomics { get; set; } = new();
    }

    public class TierProjectedEconomics
    {
        public string TierName { get; set; } = string.Empty;
        public int TotalLoadoutCost { get; set; }
        public double SuccessRatePercent { get; set; }
        public double GrossGoldPerCatch { get; set; }
        public double GrossGoldPerAttempt { get; set; }
        public double DurabilityUpkeepPerAttempt { get; set; }
        public double AccidentSinkPerAttempt { get; set; }
        public double ConsumableSinkPerAttempt { get; set; }
        public double NetGoldPerAttempt { get; set; }
        public double ProfitMarginPercent { get; set; }

        // Equipment items in this loadout
        public List<string> EquippedItems { get; set; } = new();

        // Catch Quality & Trophy Progression (Rarity, Stars & Weight)
        public double RarityBoostPercent { get; set; }
        public double StarBoostPercent { get; set; }
        public double WeightBoostPercent { get; set; }
        public double ExpectedRarePlusPercent { get; set; }
        public double ExpectedThreeStarPercent { get; set; }
        public double ExpectedTwoStarPercent { get; set; }
        public double ExpectedOneStarPercent { get; set; }
        public double ExpectedAverageWeight { get; set; }
        public Dictionary<FishRarity, double> ExpectedRarityPercentages { get; set; } = new();
    }

    public class RealEconomyTelemetry
    {
        public int TotalCatches { get; set; }
        public int TotalAttemptsRecorded { get; set; }
        public int UniqueFishers { get; set; }
        public long TotalGrossGoldEarned { get; set; }
        public double AverageGoldPerCatch { get; set; }
        public double MedianGoldPerCatch { get; set; }
        public double ObservedSuccessRatePercent { get; set; }
        public string AttemptDataSource { get; set; } = string.Empty;

        // Catch Quality & Trophy Telemetry (Observed Rarity, Stars & Weight)
        public Dictionary<FishRarity, int> ObservedRarityCounts { get; set; } = new();
        public Dictionary<FishRarity, double> ObservedRarityPercentages { get; set; } = new();
        public double ObservedRarePlusPercent { get; set; }
        public Dictionary<int, int> ObservedStarCounts { get; set; } = new();
        public double ObservedThreeStarPercent { get; set; }
        public double ObservedTwoStarPercent { get; set; }
        public double ObservedOneStarPercent { get; set; }
        public double ObservedAverageWeight { get; set; }
        public string HeaviestFishName { get; set; } = string.Empty;
        public double HeaviestFishWeight { get; set; }
        public string HeaviestFishCatcher { get; set; } = string.Empty;
        public int HeaviestFishStars { get; set; }
        public FishRarity? HeaviestFishRarity { get; set; }

        // Accident Telemetry
        public int TotalAccidentsRecorded { get; set; }
        public int RodSnapsRecorded { get; set; }
        public int LineSnapsRecorded { get; set; }
        public int ReelJamsRecorded { get; set; }
        public int TackleBoxLossesRecorded { get; set; }
        public int NetBreaksRecorded { get; set; }
        public decimal TotalGoldLostToAccidents { get; set; }
        public double AverageAccidentLossPerAttempt { get; set; }

        // Durability Upkeep Telemetry
        public double EstimatedDurabilityUpkeepIncurred { get; set; }
        public double AverageDurabilityUpkeepPerAttempt { get; set; }

        // Net Economy Flow
        public double NetGoldPerAttempt { get; set; }
        public double MedianNetGoldPerAttempt { get; set; }
        public double NetTotalGoldFlow { get; set; }

        // Player Engagement Percentiles
        public double CasualAttemptsPerSession { get; set; }
        public double ActiveAttemptsPerSession { get; set; }
        public double HardcoreAttemptsPerSession { get; set; }
        public double StreamsPerWeek { get; set; }
        public double AttemptsPerWeek { get; set; }
        public double CatchesPerWeek { get; set; }
    }

    public class EconomyVarianceSummary
    {
        public double GrossGoldCatchVariancePercent { get; set; }
        public double SuccessRateVariancePercent { get; set; }
        public double AccidentRateVariancePercent { get; set; }
        public double NetGoldAttemptVariancePercent { get; set; }
        public string SummaryNotes { get; set; } = string.Empty;
    }

    public class ItemEconomyAnalysis
    {
        public int ShopItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string EquipmentSlot { get; set; } = string.Empty;
        public int Cost { get; set; }
        public bool IsConsumable { get; set; }
        public int? MaxUses { get; set; }
        public int? MaxDurability { get; set; }
        public double DurabilityLossPerUse { get; set; }
        public bool DisableBreaking { get; set; }
        public bool IsUnbreakable { get; set; }

        // Maintenance & Operating Cost
        public double DurabilityUpkeepPerAttempt { get; set; }
        public double AccidentRiskPerAttempt { get; set; }
        public double TotalOperatingCostPerAttempt { get; set; }

        // Performance & ROI
        public double ExpectedGrossGoldBoostPerAttempt { get; set; }
        public double NetValuePerAttempt { get; set; }
        public double? PaybackAttempts { get; set; }
        public string EconomicRating { get; set; } = string.Empty; // "Profitable Investment", "Fair Upgrade", "Luxury Sink", "Consumable Sink", "Great Value"

        // Player Affordability
        public int PlayersWhoCanAfford { get; set; }
        public double PercentageWhoCanAfford { get; set; }
        public double AttemptsNeededToBuy { get; set; }
        public double SessionsToAffordCasual { get; set; }
        public double SessionsToAffordActive { get; set; }
        public double SessionsToAffordHardcore { get; set; }
        public double WeeksToAffordActive { get; set; }

        // Boost Effect Preview
        public bool HasEffectPreview { get; set; }
        public string EffectMetric { get; set; } = string.Empty;
        public double EffectBaselineValue { get; set; }
        public double EffectWithItemValue { get; set; }
        public double EffectRelativeChangePercent { get; set; }

        // Gameplay Identity & Boost Summary
        public string PrimaryBoostCategory { get; set; } = string.Empty; // "Rarity", "Stars", "Weight", "Consumable", "General"
        public string BoostSummary { get; set; } = string.Empty; // e.g. "+25% Rarity", "+20% 3-Star", "+45% Weight"
        public string TrophyRole { get; set; } = string.Empty; // e.g. "Trophy Hunting Rod", "Quality Reel", "Big Game Line"
    }

    public class LoadoutProgressionMilestone
    {
        public int Weeks { get; set; }
        public string Label { get; set; } = string.Empty;
        public List<LoadoutProgressionTier> Tiers { get; set; } = new();
    }

    public class LoadoutProgressionTier
    {
        public string TierName { get; set; } = string.Empty;
        public double AttemptsPerSession { get; set; }
        public double StreamsPerWeek { get; set; }
        public double AttemptsPerWeek { get; set; }
        public double ProjectedAttempts { get; set; }
        public double ProjectedCatches { get; set; }
        public double ProjectedGrossGold { get; set; }
        public double ProjectedUpkeepSink { get; set; }
        public double ProjectedAccidentSink { get; set; }
        public double ProjectedNetGold { get; set; }
        public double MaxGearProgressPercent { get; set; }
    }

    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error,
        Success
    }

    public class BalanceHealthDiagnostic
    {
        public DiagnosticSeverity Severity { get; set; } = DiagnosticSeverity.Info;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string RemediationAdvice { get; set; } = string.Empty;
    }

    public class BalanceSimulationScenario
    {
        public double LineSnapChance { get; set; } = 0.02;
        public double RodSnapChance { get; set; } = 0.0005;
        public double ReelJamChance { get; set; } = 0.01;
        public double TackleBoxLostChance { get; set; } = 0.005;
        public double NetBreakChance { get; set; } = 0.01;
        public double RepairCostMultiplier { get; set; } = 0.25;
        public double AttemptsPerSession { get; set; } = 30.0;
        public double StreamsPerWeek { get; set; } = 3.0;
        public string SelectedLoadoutTier { get; set; } = "Mid"; // Baseline, Entry, Mid, High, Top, Custom
        public List<int> CustomShopItemIds { get; set; } = new();
    }

    public class BalanceSimulationResult
    {
        public double SuccessRatePercent { get; set; }
        public double CombinedAccidentRatePercent { get; set; }
        public double GrossGoldPerCatch { get; set; }
        public double GrossGoldPerAttempt { get; set; }
        public double DurabilityUpkeepPerAttempt { get; set; }
        public double AccidentSinkPerAttempt { get; set; }
        public double ConsumableSinkPerAttempt { get; set; }
        public double NetGoldPerAttempt { get; set; }
        public double NetGoldPerSession { get; set; }
        public double NetGoldPerWeek { get; set; }
        public double ProfitMarginPercent { get; set; }
        public int TotalLoadoutCost { get; set; }
        public double SessionsToAffordTopGear { get; set; }
        public double WeeksToAffordTopGear { get; set; }
        public string FinancialStatus { get; set; } = string.Empty; // "Deflationary Loss", "Tight Margin", "Sustainable Growth", "High Accumulation"
        public List<string> EquippedItemNames { get; set; } = new();

        // Simulated Catch Quality
        public double RarityBoostPercent { get; set; }
        public double StarBoostPercent { get; set; }
        public double WeightBoostPercent { get; set; }
        public double ExpectedRarePlusPercent { get; set; }
        public double ExpectedThreeStarPercent { get; set; }
        public double ExpectedAverageWeight { get; set; }
        public Dictionary<FishRarity, double> ExpectedRarityPercentages { get; set; } = new();
    }
}
