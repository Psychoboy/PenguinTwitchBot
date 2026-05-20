namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class FishingBalanceReport
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TotalCatches { get; set; }
        public int UniqueUsers { get; set; }

        // Gold Economics
        public int TotalGoldEarned { get; set; }
        public double AverageGoldPerCatch { get; set; }
        public double MedianGoldPerCatch { get; set; }

        // Snap-adjusted economics
        public double ConfiguredLineSnapChance { get; set; }
        public double ConfiguredRodSnapChance { get; set; }
        public double EstimatedSuccessfulAttemptRatePercent { get; set; }
        public int EstimatedTotalAttempts { get; set; }
        public int EstimatedFailedAttempts { get; set; }
        public int EstimatedLineSnaps { get; set; }
        public int EstimatedRodSnaps { get; set; }
        public double EstimatedSnapReplacementCostPerAttempt { get; set; }
        public double EstimatedSnapReplacementCostTotal { get; set; }
        public double SnapAdjustedAverageGoldPerAttempt { get; set; }
        public double SnapAdjustedMedianGoldPerAttempt { get; set; }
        public double SnapAdjustedTotalNetGold { get; set; }
        public string AttemptDataSource { get; set; } = string.Empty;

        // Item Affordability Analysis
        public List<ItemAffordability> ItemAffordabilityAnalysis { get; set; } = new();

        // Engagement Metrics (calculated from actual attempt data)
        public double CasualAttemptsPerSession { get; set; }
        public double ActiveAttemptsPerSession { get; set; }
        public double HardcoreAttemptsPerSession { get; set; }
        public double StreamsPerWeekFromAttempts { get; set; }
        public double AttemptsPerWeek { get; set; }
        public double CatchesPerWeek { get; set; }
        public int TopGearTotalCost { get; set; }
        public List<BalanceProjectionWindow> ProjectionWindows { get; set; } = new();

        public List<string> BalanceRecommendations { get; set; } = new();
    }

    public class BalanceProjectionWindow
    {
        public int Weeks { get; set; }
        public string Label { get; set; } = string.Empty;
        public List<BalanceProjectionTier> Tiers { get; set; } = new();
    }

    public class BalanceProjectionTier
    {
        public string TierName { get; set; } = string.Empty;
        public double AttemptsPerSession { get; set; }
        public double StreamsPerWeek { get; set; }
        public double AttemptsPerWeek { get; set; }
        public double ProjectedAttempts { get; set; }
        public double ProjectedCatches { get; set; }
        public double ProjectedGrossGold { get; set; }
        public double ProjectedSnapSink { get; set; }
        public double ProjectedNetGold { get; set; }
        public double MaxGearProgressPercent { get; set; }
    }

    public class ItemAffordability
    {
        public string ItemName { get; set; } = string.Empty;
        public int Cost { get; set; }
        public bool IsConsumable { get; set; }
        public int? MaxUses { get; set; }
        public string EquipmentSlot { get; set; } = string.Empty;
        public double CostPerUse { get; set; }

        // Current affordability
        public int UsersWhoCanAfford { get; set; }
        public double PercentageWhoCanAfford { get; set; }

        // Long-term affordability (calculated from REAL player engagement percentiles)
        public double AttemptsNeededToBuy { get; set; }
        public double SessionsToAffordCasual { get; set; }  // Based on 25th percentile player
        public double SessionsToAffordActive { get; set; }  // Based on 50th percentile (median) player
        public double SessionsToAffordHardcore { get; set; } // Based on 75th percentile player

        // Value analysis
        public double MedianUserGold { get; set; }
        public string AffordabilityRating { get; set; } = string.Empty; // For permanent items
        public string ValueRating { get; set; } = string.Empty; // For consumables: "Gold Sink", "Fair Trade", "Great Value"

        // Effect preview (single-item impact reference for admin balancing)
        public bool HasEffectPreview { get; set; }
        public string EffectMetric { get; set; } = string.Empty;
        public double EffectBaselineValue { get; set; }
        public double EffectWithItemValue { get; set; }
        public double EffectRelativeChangePercent { get; set; }
    }
}
