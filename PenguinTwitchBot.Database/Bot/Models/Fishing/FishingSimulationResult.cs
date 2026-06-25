namespace PenguinTwitchBot.Database.Bot.Models.Fishing
{
    public class FishingSimulationResult
    {
        public int TotalIterations { get; set; }
        public Dictionary<FishRarity, int> RarityCounts { get; set; } = new();
        public Dictionary<string, int> FishCounts { get; set; } = new();
        public Dictionary<int, int> StarCounts { get; set; } = new();
        public double AverageWeight { get; set; }
        public double AverageGold { get; set; }
        public int TotalGold { get; set; }
        public double MinWeight { get; set; }
        public double MaxWeight { get; set; }
        public string? HeaviestFish { get; set; }
        public string? MostCommonFish { get; set; }
        public int SuccessfulCatches { get; set; }
        public int FailedAttempts { get; set; }
        public int LineSnapCount { get; set; }
        public int RodSnapCount { get; set; }
        public double SnapFailureRatePercent { get; set; }
        public double AppliedLineSnapChance { get; set; }
        public double AppliedRodSnapChance { get; set; }
        public double SnapReplacementCostTotal { get; set; }
        public double NetGoldAfterSnapCosts { get; set; }
        public double NetAverageGoldPerAttempt { get; set; }
        public bool BoostModeUsed { get; set; }
        public double BoostModeMultiplier { get; set; }
        public List<string> ItemsUsed { get; set; } = new();
    }
}
