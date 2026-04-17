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

        // Long-term affordability (based on average catches)
        public double CatchesNeededToBuy { get; set; }
        public double DaysToAfford3xWeek { get; set; } // Assuming 3 fishing sessions per week
        public double DaysToAfford1xDay { get; set; }  // Assuming 1 session per day

        // Value analysis
        public double MedianUserGold { get; set; }
        public string AffordabilityRating { get; set; } = string.Empty; // For permanent items
        public string ValueRating { get; set; } = string.Empty; // For consumables: "Gold Sink", "Fair Trade", "Great Value"
    }
}
