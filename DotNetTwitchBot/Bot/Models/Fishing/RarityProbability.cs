namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class RarityProbability
    {
        public bool BoostModeActive { get; set; }
        public double BoostModeMultiplier { get; set; }
        public List<string> ItemsEquipped { get; set; } = new();
        public Dictionary<FishRarity, double> Probabilities { get; set; } = new();
    }
}
