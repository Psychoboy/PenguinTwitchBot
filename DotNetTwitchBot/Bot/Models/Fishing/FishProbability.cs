namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class FishProbability
    {
        public int FishId { get; set; }
        public string FishName { get; set; } = string.Empty;
        public FishRarity Rarity { get; set; }
        public double RarityChance { get; set; }
        public double WithinRarityChance { get; set; }
        public double OverallChance { get; set; }
        public int ExpectedAttemptsForOneCatch { get; set; }
    }
}
