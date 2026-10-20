using PenguinTwitchBot.Bot.Commands.Games.Common;

namespace PenguinTwitchBot.Bot.Commands.Games.Curling
{
    public static class CurlingSimulationEngine
    {
        public const double TickRateHz = SlideShotPhysics.TickRateHz;
        public const double Dt = 1.0 / TickRateHz;

        // Geometric radii normalized relative to the primary slide axis length [0.0, 1.0]
        public const double ButtonBullseyeRadius = 0.022; // Center 100 pt button
        public const double FourFootRadius = 0.048;      // 75 pt inner ring
        public const double EightFootRadius = 0.082;     // 50 pt middle ring
        public const double TwelveFootRadius = 0.125;    // 25 pt outer ring

        // Baseline friction deceleration on normal ice
        public const double BaseFrictionDecel = SlideShotPhysics.BaseDecel;

        /// <summary>
        /// Maps an IceCondition to a friction drag multiplier.
        /// Low drag = slick (slides further with less power), High drag = heavy frost (requires more muscle).
        /// </summary>
        public static double ResolveIceDragMultiplier(CurlingIceCondition condition) => condition switch
        {
            CurlingIceCondition.Slick => 0.78,       // 22% less friction (glides far)
            CurlingIceCondition.HeavyFrost => 1.28,  // 28% more friction (stops quickly)
            CurlingIceCondition.CrossDrift => 1.05,  // Slightly slower with lateral pull
            _ => 1.00                                // Standard normal ice
        };

        /// <summary>
        /// Calculates the effective power applying streamer variance and bounds clamping.
        /// </summary>
        public static double CalculateEffectivePower(double rawPower, double variancePercent, Random? rng = null)
            => SlideShotPhysics.CalculateEffectivePower(rawPower, variancePercent, rng);

        /// <summary>
        /// Converts an effective power (1-100) into initial launch speed.
        /// Calibrated so that power ~75-80 lands directly on standard target pin (~84%) on normal ice.
        /// </summary>
        public static double PowerToInitialSpeed(double effectivePower, double dragMultiplier)
            => SlideShotPhysics.PowerToInitialSpeed(effectivePower);

        /// <summary>
        /// Deterministically computes the total slide distance from initial launch speed and drag.
        /// </summary>
        public static double CalculateSlideDistance(double initialSpeed, double dragMultiplier)
            => SlideShotPhysics.CalculateTravelDistance(initialSpeed, dragMultiplier);

        /// <summary>
        /// Simulates a curling slide trajectory, populating keyframes, rest coordinates, score, and bullseye status.
        /// </summary>
        public static void SimulateCurlingSlide(
            PendingDropRecord record,
            CurlingGameSettings settings,
            double rawPower,
            CurlingIceCondition condition,
            double targetDistancePercent,
            Random? rng = null,
            bool? isReversedOverride = null)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(settings);

            var dragMultiplier = ResolveIceDragMultiplier(condition);
            var effectivePower = CalculateEffectivePower(rawPower, settings.PowerVariancePercent, rng);
            record.EffectivePower = effectivePower;
            var v0 = PowerToInitialSpeed(effectivePower, dragMultiplier);
            var decel = BaseFrictionDecel * dragMultiplier;
            var slideDist = CalculateSlideDistance(v0, dragMultiplier);

            // Determine orientation: horizontal vs vertical
            var isHorizontal = settings.StripPosition is CurlingStripPosition.Bottom or CurlingStripPosition.Top;
            var isReversed = SlideShotPhysics.ResolveIsReversed(settings.SlideDirection, record.DropToken, isReversedOverride);

            var startPos = SlideShotPhysics.DefaultTeePosition(isReversed);
            var directionSign = isReversed ? -1.0 : 1.0;
            var restPos = Math.Clamp(startPos + directionSign * slideDist, 0.02, 0.98);

            // Calculate rest ticks: v(t) = v0 - decel * t = 0 => t = v0 / decel
            var durationSec = v0 / decel;
            var totalTicks = (int)Math.Max(30, Math.Round(durationSec * TickRateHz));
            record.TotalTicks = totalTicks;

            var lateralOffset = 0.50; // Centered in lane strip
            if (condition == CurlingIceCondition.CrossDrift)
            {
                var r = rng ?? Random.Shared;
                var driftDir = r.Next(0, 2) == 0 ? -1.0 : 1.0;
                lateralOffset = Math.Clamp(0.50 + driftDir * 0.18 * (effectivePower / 100.0), 0.15, 0.85);
            }

            // Target position normalized along slide axis
            var normalizedTarget = SlideShotPhysics.ResolveNormalizedTarget(settings.SlideDirection, targetDistancePercent, isReversed);

            // Keyframes for server/client synchronization
            record.Keyframes =
            [
                SlideShotPhysics.CreatePoint(0, startPos, lateralOffset, 0.0, v0 * directionSign, 0.0, isHorizontal),
                SlideShotPhysics.CreatePoint(totalTicks, restPos, lateralOffset, 0.0, 0.0, 0.0, isHorizontal)
            ];

            // Evaluate rest score and landing coordinates
            record.LandingX = isHorizontal ? restPos : lateralOffset;
            var restAxisPos = restPos;
            var (score, isBullseye, isWin) = CalculateScore(restAxisPos, normalizedTarget, settings.ScoringType, isReversed, settings.EnableShuffleboardKitchenPenalty);

            record.Score = score;
            record.IsBullseye = isBullseye;
            record.IsWin = isWin;
            record.IsHit = isWin;
        }

        /// <summary>
        /// Evaluates the final resting position relative to the target center.
        /// </summary>
        public static (int Score, bool IsBullseye, bool IsWin) CalculateScore(
            double restPos,
            double targetPos,
            CurlingScoringType scoringType,
            bool isReversed = false,
            bool enableKitchenPenalty = true)
        {
            var delta = Math.Abs(restPos - targetPos);

            if (scoringType == CurlingScoringType.Shuffleboard)
            {
                // Shuffleboard tiered zones along linear runway
                // dirSign * (restPos - targetPos):
                //   negative = short of target (approaching)
                //   positive = overshot target (past apex)
                var dirSign = isReversed ? -1.0 : 1.0;
                var offset = dirSign * (restPos - targetPos);
                const double bandW = 0.050;

                if (offset >= -0.5 * bandW && offset <= 0.5 * bandW)
                {
                    return (30, true, true); // Zone 30 Bullseye
                }
                if (offset >= -1.5 * bandW && offset < -0.5 * bandW)
                {
                    return (20, false, true); // Zone 20
                }
                if (offset >= -2.5 * bandW && offset < -1.5 * bandW)
                {
                    return (10, false, true); // Zone 10
                }
                if (enableKitchenPenalty && offset > 0.5 * bandW && offset <= 1.7 * bandW)
                {
                    return (-10, false, false); // In the Kitchen penalty (-10)
                }
                return (0, false, false); // Short or out the back
            }

            // Curling House Mode (concentric circles around Button)
            if (delta <= ButtonBullseyeRadius)
            {
                return (100, true, true); // Button bullseye
            }
            if (delta <= FourFootRadius)
            {
                return (75, false, true); // 4-Foot ring
            }
            if (delta <= EightFootRadius)
            {
                return (50, false, true); // 8-Foot ring
            }
            if (delta <= TwelveFootRadius)
            {
                return (25, false, true); // 12-Foot ring
            }

            return (0, false, false); // Out of the house (foul/miss)
        }
    }
}

