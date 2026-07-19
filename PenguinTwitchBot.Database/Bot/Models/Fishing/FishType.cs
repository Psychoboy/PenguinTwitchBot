using PenguinTwitchBot.Database.Bot.Models.Points;
using System.ComponentModel.DataAnnotations;

namespace PenguinTwitchBot.Database.Bot.Models.Fishing
{
    public class FishType
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public FishRarity Rarity { get; set; } = FishRarity.Common;
        public double BaseWeight { get; set; } = 5.0;
        public int BaseGold { get; set; } = 10;
        public string ImageFileName { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;

        public ICollection<FishCategory> Categories { get; set; } = [];
    }

    public class FishCategory
    {
        [Key]
        public int Id { get; set; }

        public int FishTypeId { get; set; }
        public FishType FishType { get; set; } = null!;

        [MaxLength(128)]
        public string Category { get; set; } = string.Empty;
    }

    public enum FishRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    public enum FishingTournamentStatus
    {
        Draft = 0,
        Scheduled = 1,
        Active = 2,
        Completed = 3,
        Cancelled = 4
    }

    public enum FishingTournamentScoreCategory
    {
        SpecificFish = 0,
        Largest = 1,
        MostValuable = 2,
        Smallest = 3,
        Average = 4,
        MostCatches = 5,
        TotalWeight = 6
    }

    public enum FishingTournamentRewardKind
    {
        Points = 0,
        EntryFeePercentage = 1
    }

    public class FishingTournament
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1024)]
        public string Description { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;
        public FishingTournamentStatus Status { get; set; } = FishingTournamentStatus.Draft;
        public FishingTournamentScoreCategory PrimaryScoreCategory { get; set; } = FishingTournamentScoreCategory.Largest;
        public DateTime? StartsAtUtc { get; set; }
        public DateTime? EndsAtUtc { get; set; }
        public bool AutoScheduleEnabled { get; set; }
        public string AutoScheduleCron { get; set; } = string.Empty;
        public int RunDurationMinutes { get; set; } = 60;
        public long? EntryFeeAmount { get; set; }
        public int? EntryFeePointTypeId { get; set; }
        public PointType? EntryFeePointType { get; set; }

        public ICollection<FishingTournamentFishType> EligibleFish { get; set; } = [];
        public ICollection<FishingTournamentEligibleCategory> EligibleCategories { get; set; } = [];
        public ICollection<FishingTournamentRewardRule> RewardRules { get; set; } = [];
    }

    public class FishingTournamentFishType
    {
        [Key]
        public int Id { get; set; }

        public int FishingTournamentId { get; set; }
        public FishingTournament FishingTournament { get; set; } = null!;

        public int FishTypeId { get; set; }
        public FishType FishType { get; set; } = null!;
    }

    public class FishingTournamentEligibleCategory
    {
        [Key]
        public int Id { get; set; }

        public int FishingTournamentId { get; set; }
        public FishingTournament FishingTournament { get; set; } = null!;

        [MaxLength(128)]
        public string Category { get; set; } = string.Empty;
    }

    public class FishingTournamentRewardRule
    {
        [Key]
        public int Id { get; set; }

        public int FishingTournamentId { get; set; }
        public FishingTournament FishingTournament { get; set; } = null!;

        public FishingTournamentScoreCategory ScoreCategory { get; set; } = FishingTournamentScoreCategory.Largest;
        public int? TargetFishTypeId { get; set; }
        public FishType? TargetFishType { get; set; }
        public FishingTournamentRewardKind RewardKind { get; set; } = FishingTournamentRewardKind.Points;
        public int Placement { get; set; } = 1;
        public long Points { get; set; } = 0;
        public int? EntryFeePercentage { get; set; }
        public int PointTypeId { get; set; }
        public PointType PointType { get; set; } = null!;
        public bool Enabled { get; set; } = true;
    }
}
