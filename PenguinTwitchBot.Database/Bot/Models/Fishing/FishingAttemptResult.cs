namespace PenguinTwitchBot.Database.Bot.Models.Fishing
{
    public class FishingAttemptResult
    {
        public FishingAttemptOutcome Outcome { get; set; } = FishingAttemptOutcome.CaughtFish;
        public FishCatch? FishCatch { get; set; }
        public List<EquipmentSlot> LostEquipmentSlots { get; set; } = new();

        public bool IsSuccessfulCatch => Outcome == FishingAttemptOutcome.CaughtFish && FishCatch != null;
        public List<FishingBrokenItemInfo> BrokenItems { get; set; } = new();
        public FishingSnapEvent? SnapEvent { get; set; }

        // Enriched catch context for overlay badges, chat, and action variables
        public bool IsNewSpecies { get; set; }
        public bool IsPersonalBest { get; set; }
        public double? PreviousBestWeight { get; set; }
        public bool IsServerRecord { get; set; }
        public double? PreviousServerRecordWeight { get; set; }
        public bool IsFrenzyBonus { get; set; }
        public string? ActiveFrenzyName { get; set; }
        public List<FishingTriggeredBoostInfo> TriggeredBoosts { get; set; } = new();
        public List<FishingTournamentCatchSummary> MatchedTournaments { get; set; } = new();
        public List<FishingPointRewardAwarded> PointRewardsAwarded { get; set; } = new();
    }

    public enum FishingAttemptOutcome
    {
        CaughtFish,
        LineSnapped,
        RodSnapped,
        ReelJammed,
        TackleBoxLost,
        NetBroken
    }
}