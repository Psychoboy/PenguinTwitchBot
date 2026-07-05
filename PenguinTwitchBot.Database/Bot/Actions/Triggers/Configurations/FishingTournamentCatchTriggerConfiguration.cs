namespace PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations
{
    public class FishingTournamentCatchTriggerConfiguration
    {
        public bool RequireEligibleTournamentFish { get; set; } = true;
        public bool RequireQualifyingPosition { get; set; }
        public int QualifyingPlacementOverride { get; set; }
    }
}
