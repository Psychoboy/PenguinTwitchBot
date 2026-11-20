namespace PenguinTwitchBot.Database.Bot.Models.Fishing
{
    public enum FishingCatchRewardMode
    {
        Flat = 0,
        GoldScaled = 1,
        WeightScaled = 2
    }

    /// <summary>
    /// Configurable built-in multi-point reward rule evaluated on each successful fish catch.
    /// Serialized as JSON inside <see cref="FishingSettings.CatchRewardsJson"/>.
    /// </summary>
    public class FishingCatchRewardRule
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public bool Enabled { get; set; } = true;
        public Guid PointTypeId { get; set; }
        public FishRarity? MinRarity { get; set; }
        public FishRarity? MaxRarity { get; set; }
        public string? CategoryFilter { get; set; }
        public FishingCatchRewardMode Mode { get; set; } = FishingCatchRewardMode.GoldScaled;

        /// <summary>Flat point amount when Mode == Flat.</summary>
        public long FlatAmount { get; set; } = 50;

        /// <summary>Base multiplier for GoldScaled (Gold * BaseMultiplier * StarMultiplier) or WeightScaled (Weight * BaseMultiplier * StarMultiplier).</summary>
        public double BaseMultiplier { get; set; } = 1.0;

        public double Star1Multiplier { get; set; } = 1.0;
        public double Star2Multiplier { get; set; } = 2.0;
        public double Star3Multiplier { get; set; } = 4.0;

        public long? MinAmount { get; set; }
        public long? MaxAmount { get; set; }

        /// <summary>Optional extra percentage bonus when catching a species for the first time (e.g., 50 = +50%).</summary>
        public double NewDexBonusPercent { get; set; } = 0;

        /// <summary>Optional extra percentage bonus when setting a Personal Best weight (e.g., 25 = +25%).</summary>
        public double PersonalBestBonusPercent { get; set; } = 0;
    }

    public class FishingPointRewardAwarded
    {
        public Guid PointTypeId { get; set; }
        public string PointTypeName { get; set; } = string.Empty;
        public long Amount { get; set; }
    }

    public class FishingTournamentCatchSummary
    {
        public Guid TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;
        public int NewRank { get; set; }
        public bool IsLeader { get; set; }
        public bool TookLead { get; set; }
        public string? PreviousLeaderUsername { get; set; }
        public double Score { get; set; }
    }

    public class FishingTriggeredBoostInfo
    {
        public string ItemName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public int? RemainingUses { get; set; }
        public int? MaxUses { get; set; }
    }

    /// <summary>
    /// Reusable multi-point payout entry for Quick / Blitz Tournaments,
    /// serialized inside <see cref="FishingSettings.QuickTournamentRewardsJson"/>.
    /// </summary>
    public class QuickTournamentRewardEntry
    {
        public int Placement { get; set; } = 1;
        public Guid PointTypeId { get; set; }
        public long Points { get; set; }
        public int? GoldAmount { get; set; }
    }

    public class UserFishingDexSummary
    {
        public int CaughtSpeciesCount { get; set; }
        public int TotalSpeciesCount { get; set; }
        public int TotalCatches { get; set; }
        public int TotalGold { get; set; }
        public string? BestCatchFishName { get; set; }
        public double BestCatchWeight { get; set; }
        public int BestCatchStars { get; set; }
        public int BestCatchGold { get; set; }
        public double CompletionPercent => TotalSpeciesCount > 0
            ? Math.Round((double)CaughtSpeciesCount / TotalSpeciesCount * 100.0, 1)
            : 0;
    }
}
