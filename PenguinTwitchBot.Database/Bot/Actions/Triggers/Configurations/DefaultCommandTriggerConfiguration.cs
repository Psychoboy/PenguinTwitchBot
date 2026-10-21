namespace PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations
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

        // Parachute Drop events
        public const string ParachuteStart = "Parachute.Start";
        public const string ParachuteStarted = "Parachute.Start";
        public const string ParachuteWin = "Parachute.Win";
        public const string ParachuteBullseye = "Parachute.Bullseye";
        public const string ParachuteLose = "Parachute.Lose";
        public const string ParachuteEnd = "Parachute.End";
        public const string ParachuteEnded = "Parachute.End";
        public const string ParachuteRocketKill = "Parachute.RocketKill";

        // Plinko Bounce events
        public const string PlinkoStart = "Plinko.Start";
        public const string PlinkoStarted = "Plinko.Start";
        public const string PlinkoWin = "Plinko.Win";
        public const string PlinkoLose = "Plinko.Lose";
        public const string PlinkoEnd = "Plinko.End";
        public const string PlinkoEnded = "Plinko.End";

        // Balloon Ascent events
        public const string BalloonStart = "Balloon.Start";
        public const string BalloonStarted = "Balloon.Start";
        public const string BalloonWin = "Balloon.Win";
        public const string BalloonBullseye = "Balloon.Bullseye";
        public const string BalloonLose = "Balloon.Lose";
        public const string BalloonEnd = "Balloon.End";
        public const string BalloonEnded = "Balloon.End";
        public const string BalloonPopped = "Balloon.Popped";
        public const string BalloonPop = "Balloon.Popped";

        // Curling / Shuffleboard events
        public const string CurlingStart = "Curling.Start";
        public const string CurlingStarted = "Curling.Start";
        public const string CurlingWin = "Curling.Win";
        public const string CurlingBullseye = "Curling.Bullseye";
        public const string CurlingLose = "Curling.Lose";
        public const string CurlingEnd = "Curling.End";
        public const string CurlingEnded = "Curling.End";

        // Golf Chip & Putt events
        public const string GolfStart = "Golf.Start";
        public const string GolfStarted = "Golf.Start";
        public const string GolfWin = "Golf.Win";
        public const string GolfBullseye = "Golf.Bullseye";
        public const string GolfLose = "Golf.Lose";
        public const string GolfEnd = "Golf.End";
        public const string GolfEnded = "Golf.End";

        // Starship Expedition events
        public const string StarshipBoardingOpened = "Starship.BoardingOpened";
        public const string StarshipCrewJoined = "Starship.CrewJoined";
        public const string StarshipEventOpened = "Starship.EventOpened";
        public const string StarshipEventResolved = "Starship.EventResolved";
        public const string StarshipVoyageEnded = "Starship.VoyageEnded";
        public const string StarshipCrewRewardBanked = "Starship.CrewRewardBanked";
        public const string StarshipRiskSucceeded = "Starship.RiskSucceeded";
        public const string StarshipRiskFailed = "Starship.RiskFailed";
        public const string StarshipEventStepAdvanced = "Starship.EventStepAdvanced";
        public const string StarshipEventSucceeded = "Starship.EventSucceeded";
        public const string StarshipEventFailed = "Starship.EventFailed";
    }
}
