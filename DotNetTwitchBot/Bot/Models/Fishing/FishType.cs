using System.ComponentModel.DataAnnotations;

namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class FishType
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public FishRarity Rarity { get; set; } = FishRarity.Common;
        public double MinWeight { get; set; } = 1.0;
        public double MaxWeight { get; set; } = 10.0;
        public int BaseGold { get; set; } = 10;
        public string ImageFileName { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }

    public enum FishRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }
}
