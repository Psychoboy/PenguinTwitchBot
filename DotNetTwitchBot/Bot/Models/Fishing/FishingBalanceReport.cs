namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class FishingBalanceReport
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TotalCatches { get; set; }
        public int UniqueUsers { get; set; }
        
        // Rarity Distribution
        public Dictionary<FishRarity, int> RarityDistribution { get; set; } = new();
        public Dictionary<FishRarity, double> RarityPercentages { get; set; } = new();
        
        // Expected vs Actual (if boost mode settings provided)
        public Dictionary<FishRarity, double> ExpectedRarityPercentages { get; set; } = new();
        public Dictionary<FishRarity, double> RarityVariance { get; set; } = new();
        
        // Star Distribution
        public Dictionary<int, int> StarDistribution { get; set; } = new();
        public Dictionary<int, double> StarPercentages { get; set; } = new();
        
        // Gold Economics
        public int TotalGoldEarned { get; set; }
        public double AverageGoldPerCatch { get; set; }
        public double MedianGoldPerCatch { get; set; }
        public int MinGoldEarned { get; set; }
        public int MaxGoldEarned { get; set; }

        // Snap-adjusted economics (estimates derived from configured snap rates)
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
        
        // Per-User Statistics
        public double AverageCatchesPerUser { get; set; }
        public double AverageGoldPerUser { get; set; }
        public Dictionary<string, int> TopUsersByCatches { get; set; } = new();
        public Dictionary<string, int> TopUsersByGold { get; set; } = new();
        
        // Fish Distribution
        public Dictionary<string, int> FishCatchCounts { get; set; } = new();
        public Dictionary<string, double> FishCatchPercentages { get; set; } = new();
        public string? MostCaughtFish { get; set; }
        public string? LeastCaughtFish { get; set; }
        
        // Item Affordability Analysis
        public List<ItemAffordability> ItemAffordabilityAnalysis { get; set; } = new();
        
        // Boost Mode Info
        public bool? BoostModeActive { get; set; }
        public double? BoostModeMultiplier { get; set; }
        
        // Most Common Equipment Used
        public Dictionary<string, int> MostCommonEquipment { get; set; } = new();

        // Engagement Metrics (calculated from actual player data)
        public double CasualCatchesPerSession { get; set; }    // 25th percentile
        public double ActiveCatchesPerSession { get; set; }     // 50th percentile (median)
        public double HardcoreCatchesPerSession { get; set; }  // 75th percentile

        // Summary Statistics
        public string Summary { get; set; } = string.Empty;
        public List<string> BalanceRecommendations { get; set; } = new();
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
        public double CatchesNeededToBuy { get; set; }
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
