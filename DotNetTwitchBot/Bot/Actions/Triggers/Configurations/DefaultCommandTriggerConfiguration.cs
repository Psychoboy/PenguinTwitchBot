namespace DotNetTwitchBot.Bot.Actions.Triggers.Configurations
{
    public class DefaultCommandTriggerConfiguration
    {
        public string DefaultCommandName { get; set; } = null!; // e.g., "gamble", "defuse"
        public string EventType { get; set; } = null!; // e.g., "Gamble.JackpotWin", "Defuse.Success"
    }

    public static class DefaultCommandEventTypes
    {
        // Gamble events
        public const string GambleJackpotWin = "Gamble.JackpotWin";
        public const string GambleWin = "Gamble.Win";
        public const string GambleLose = "Gamble.Lose";

        // Defuse events
        public const string DefuseSuccess = "Defuse.Success";
        public const string DefuseFailure = "Defuse.Failure";

        // Roll events
        public const string RollDoubles = "Roll.Doubles";
        public const string RollSnakeEyes = "Roll.SnakeEyes";
        public const string RollBoxcars = "Roll.Boxcars";
        public const string RollLose = "Roll.Lose";

        // Slots events
        public const string SlotsThreeOfAKind = "Slots.ThreeOfAKind";
        public const string SlotsTwoOfAKind = "Slots.TwoOfAKind";
        public const string SlotsLose = "Slots.Lose";

        // Steal events
        public const string StealSuccess = "Steal.Success";
        public const string StealFailed = "Steal.Failed";
        public const string StealToPoor = "Steal.ToPoor";

        // Heist events
        public const string HeistStarted = "Heist.Started";
        public const string HeistUserSurvived = "Heist.UserSurvived";
        public const string HeistUserCaught = "Heist.UserCaught";
        public const string HeistEnded = "Heist.Ended";

        // DeathCounter events
        public const string DeathIncremented = "Death.Incremented";
        public const string DeathDecremented = "Death.Decremented";
        public const string DeathReset = "Death.Reset";
        public const string DeathSet = "Death.Set";

        // Wheel events
        public const string WheelSpinResult = "WheelSpin.Result";
    }
}
