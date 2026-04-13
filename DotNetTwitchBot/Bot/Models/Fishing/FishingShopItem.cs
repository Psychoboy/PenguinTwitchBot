using System.ComponentModel.DataAnnotations;

namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class FishingShopItem
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Cost { get; set; } = 100;
        public FishingBoostType BoostType { get; set; } = FishingBoostType.GeneralRarityBoost;
        public double BoostAmount { get; set; } = 0.05;
        public int? TargetFishTypeId { get; set; }
        public virtual FishType? TargetFishType { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public enum FishingBoostType
    {
        GeneralRarityBoost,
        SpecificFishBoost,
        WeightBoost,
        StarBoost
    }
}
