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