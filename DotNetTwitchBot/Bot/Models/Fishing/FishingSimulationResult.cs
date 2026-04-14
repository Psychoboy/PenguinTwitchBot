namespace DotNetTwitchBot.Bot.Models.Fishing
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
        public bool BoostModeUsed { get; set; }
        public double BoostModeMultiplier { get; set; }
        public List<string> ItemsUsed { get; set; } = new();
    }
}
