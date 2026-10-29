namespace PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations
{
    public class DefaultCommandTriggerConfiguration
    {
        public string DefaultCommandName { get; set; } = null!; // e.g., "gamble", "defuse"
        public string EventType { get; set; } = null!; // e.g., "Gamble.JackpotWin", "Defuse.Success"

        // Racing Filters
        /// <summary>
        /// Specific placements that trigger this event (e.g. [1] for 1st place, [2] for 2nd, [1, 2, 3] for podium).
        /// Empty means any placement.
        /// </summary>
        public List<int> Placements { get; set; } = [];

        /// <summary>
        /// Minimum placement required (e.g. 4 for 4th place or worse / participation). 0 or null = no minimum.
        /// </summary>
        public int? MinPlacement { get; set; }

        /// <summary>
        /// Maximum placement allowed (e.g. 3 for top 3). 0 or null = no maximum.
        /// </summary>
        public int? MaxPlacement { get; set; }

        /// <summary>
        /// Filter by racing game modes (e.g. "GrandPrix", "DestructionDerby", "TimeTrial"). Empty = all modes.
        /// </summary>
        public List<string> RacingGameModes { get; set; } = [];

        /// <summary>
        /// Filter by outcome condition: "Any", "Survived", "Totaled", "CleanRace" (0 wrecks).
        /// </summary>
        public string? RacingOutcomeFilter { get; set; }

        /// <summary>
        /// Minimum knockouts required in Destruction Derby (null or 0 = any).
        /// </summary>
        public int? MinKnockouts { get; set; }

        /// <summary>
        /// Wheel Spin / UFO Claw: only fire when the winning prize label equals this (case-insensitive). Empty = any prize.
        /// </summary>
        public string? PrizeLabel { get; set; }

        /// <summary>
        /// Golf shot filter: "HoleInOne", "NotHoleInOne", or null/"Any".
        /// </summary>
        public string? GolfHoleInOneFilter { get; set; }

        /// <summary>
        /// Evaluates whether this trigger configuration matches the execution context variables.
        /// When a filter is actively configured, the required context variable must be present and valid.
        /// </summary>
        public bool MatchesVariables(IReadOnlyDictionary<string, string>? variables)
        {
            if (!string.IsNullOrWhiteSpace(GolfHoleInOneFilter) && !string.Equals(GolfHoleInOneFilter, "Any", StringComparison.OrdinalIgnoreCase))
            {
                if (variables == null ||
                    !variables.TryGetValue("IsHoleInOne", out var isHoleInOneStr) ||
                    !bool.TryParse(isHoleInOneStr, out var isHoleInOne))
                    return false;

                if (string.Equals(GolfHoleInOneFilter, "HoleInOne", StringComparison.OrdinalIgnoreCase) && !isHoleInOne)
                    return false;

                if (string.Equals(GolfHoleInOneFilter, "NotHoleInOne", StringComparison.OrdinalIgnoreCase) && isHoleInOne)
                    return false;

                if (!string.Equals(GolfHoleInOneFilter, "HoleInOne", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(GolfHoleInOneFilter, "NotHoleInOne", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (variables == null) return true;

            // 1. Placement filter
            if (Placements != null && Placements.Count > 0)
            {
                if (!variables.TryGetValue("Placement", out var placementStr) || !int.TryParse(placementStr, out var placement) || !Placements.Contains(placement))
                    return false;
            }

            if (MinPlacement.HasValue && MinPlacement.Value > 0)
            {
                if (!variables.TryGetValue("Placement", out var placementStr) || !int.TryParse(placementStr, out var placement) || placement < MinPlacement.Value)
                    return false;
            }

            if (MaxPlacement.HasValue && MaxPlacement.Value > 0)
            {
                if (!variables.TryGetValue("Placement", out var placementStr) || !int.TryParse(placementStr, out var placement) || placement > MaxPlacement.Value)
                    return false;
            }

            // 2. Racing Game Mode filter
            if (RacingGameModes != null && RacingGameModes.Count > 0)
            {
                string? activeMode = null;
                if (variables.TryGetValue("RacingMode", out var mode) || variables.TryGetValue("GameMode", out mode))
                {
                    activeMode = mode;
                }

                if (string.IsNullOrWhiteSpace(activeMode) || !RacingGameModes.Any(m => string.Equals(m, activeMode, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            // 3. Racing Outcome condition
            if (!string.IsNullOrWhiteSpace(RacingOutcomeFilter) && !string.Equals(RacingOutcomeFilter, "Any", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(RacingOutcomeFilter, "Survived", StringComparison.OrdinalIgnoreCase))
                {
                    if (!variables.TryGetValue("IsTotaled", out var isTotaledStr) || !bool.TryParse(isTotaledStr, out var isTotaled) || isTotaled)
                        return false;
                }
                else if (string.Equals(RacingOutcomeFilter, "Totaled", StringComparison.OrdinalIgnoreCase))
                {
                    if (!variables.TryGetValue("IsTotaled", out var isTotaledStr) || !bool.TryParse(isTotaledStr, out var isTotaled) || !isTotaled)
                        return false;
                }
                else if (string.Equals(RacingOutcomeFilter, "CleanRace", StringComparison.OrdinalIgnoreCase))
                {
                    if (!variables.TryGetValue("WreckCount", out var wreckStr) || !int.TryParse(wreckStr, out var wrecks) || wrecks > 0)
                        return false;
                }
                else if (string.Equals(RacingOutcomeFilter, "FirstPlace", StringComparison.OrdinalIgnoreCase))
                {
                    if (!variables.TryGetValue("Placement", out var pStr) || !int.TryParse(pStr, out var p) || p != 1)
                        return false;
                }
                else if (string.Equals(RacingOutcomeFilter, "Podium", StringComparison.OrdinalIgnoreCase))
                {
                    if (!variables.TryGetValue("Placement", out var pStr) || !int.TryParse(pStr, out var p) || p > 3)
                        return false;
                }
                else
                {
                    return false;
                }
            }

            // 4. Knockout count filter
            if (MinKnockouts.HasValue && MinKnockouts.Value > 0)
            {
                if (!variables.TryGetValue("KnockoutCount", out var koStr) || !int.TryParse(koStr, out var knockouts) || knockouts < MinKnockouts.Value)
                    return false;
            }

            // 5. Prize label filter
            if (!string.IsNullOrWhiteSpace(PrizeLabel))
            {
                if (!variables.TryGetValue("WinningLabel", out var label) || !string.Equals(label?.Trim(), PrizeLabel.Trim(), StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
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
        public const string UfoClawResult = "UfoClaw.Result";

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

        // Racing events
        public const string RacingLobbyOpened = "Racing.LobbyOpened";
        public const string RacingStart = "Racing.Start";
        public const string RacingStarted = "Racing.Start";
        public const string RacingWin = "Racing.Win";
        public const string RacingPodium = "Racing.Podium";
        public const string RacingWreck = "Racing.Wreck";
        public const string RacingDerbyKnockout = "Racing.DerbyKnockout";
        public const string RacingEnd = "Racing.End";
        public const string RacingEnded = "Racing.End";
        public const string RacingComplete = "Racing.Complete";
        public const string RacingCompleted = "Racing.Complete";
        public const string RacingTimeTrialComplete = "Racing.TimeTrialComplete";

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
