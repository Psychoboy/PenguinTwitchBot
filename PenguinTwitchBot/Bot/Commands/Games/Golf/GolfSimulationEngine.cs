using PenguinTwitchBot.Bot.Commands.Games.Common;

namespace PenguinTwitchBot.Bot.Commands.Games.Golf
{
    public static class GolfSimulationEngine
    {
        public const double TickRateHz = SlideShotPhysics.TickRateHz;
        public const double Dt = 1.0 / TickRateHz;

        // Geometric radii normalized relative to the primary fairway axis length [0.0, 1.0]
        // Calibrated with client overlay green circle (~44% of strip breadth / ~3.5% of course length)
        public const double CupRadius = 0.014;          // Center sunken cup
        public const double InnerRingRadius = 0.018;    // Visual inner ring guide (~75 pts)
        public const double MiddleRingRadius = 0.026;   // Visual middle ring guide (~50 pts)
        public const double GreenRadius = 0.035;        // Putting green outer boundary (~3.5 yd)

        // Baseline turf deceleration on a standard green
        public const double BaseTurfDecel = SlideShotPhysics.BaseDecel;

        // Baseline max speed at which the cup can capture a rolling ball without lipping out
        public const double BaseMaxCaptureSpeed = 0.070;

        /// <summary>
        /// Maps a GolfGreenCondition to a turf friction drag multiplier.
        /// Low drag = Fast Links (rolls further with less power), High drag = Heavy Dew (requires more club power).
        /// </summary>
        public static double ResolveGreenDragMultiplier(GolfGreenCondition condition) => condition switch
        {
            GolfGreenCondition.FastLinks => 0.78,     // 22% less friction (firm links turf)
            GolfGreenCondition.HeavyDew => 1.28,      // 28% more friction (damp morning grass)
            GolfGreenCondition.CrossBreeze => 1.05,   // Slightly slower with lateral wind push
            _ => 1.00                                 // Standard Green
        };

        /// <summary>
        /// Calculates the effective swing power applying streamer variance and bounds clamping.
        /// </summary>
        public static double CalculateEffectivePower(double rawPower, double variancePercent, Random? rng = null)
            => SlideShotPhysics.CalculateEffectivePower(rawPower, variancePercent, rng);

        /// <summary>
        /// Converts an effective swing power (1-100) into initial launch speed.
        /// Calibrated so that power ~78 lands right at the standard pin (~84%) on a Standard Green.
        /// </summary>
        public static double PowerToInitialSpeed(double effectivePower, double dragMultiplier)
            => SlideShotPhysics.PowerToInitialSpeed(effectivePower);

        /// <summary>
        /// Deterministically computes total shot travel distance (carry + bounce + roll) from initial launch speed and turf drag.
        /// </summary>
        public static double CalculateShotDistance(double initialSpeed, double dragMultiplier)
            => SlideShotPhysics.CalculateTravelDistance(initialSpeed, dragMultiplier);

        /// <summary>
        /// Resolves the appropriate golf club ("Driver", "Wedge", "Putter") based on stroke number and distance to the green.
        /// </summary>
        public static string ResolveClub(int strokeNumber, double distToPin, bool? isOnGreen = null, double? greenRadius = null)
        {
            if (strokeNumber <= 1) return "Driver";
            if (isOnGreen.HasValue) return isOnGreen.Value ? "Putter" : "Wedge";
            var effectiveRadius = (greenRadius.HasValue && greenRadius.Value > 0) ? greenRadius.Value : GreenRadius;
            return distToPin <= effectiveRadius ? "Putter" : "Wedge";
        }

        /// <summary>
        /// Simulates a golf chip or putt trajectory for headless physics tests. Runtime outcomes are client-authoritative.
        /// </summary>
        public static void SimulateGolfShot(
            PendingDropRecord record,
            GolfGameSettings settings,
            double rawPower,
            GolfGreenCondition condition,
            double targetDistancePercent,
            int strokeNumber = 1,
            double? startAxisPosOverride = null,
            Random? rng = null,
            bool? isReversedOverride = null)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(settings);

            strokeNumber = Math.Max(1, strokeNumber);
            record.StrokeNumber = strokeNumber;

            var dragMultiplier = ResolveGreenDragMultiplier(condition);
            var effectivePower = CalculateEffectivePower(rawPower, settings.PowerVariancePercent, rng);
            var decel = Math.Max(0.01, BaseTurfDecel * dragMultiplier);

            var isHorizontal = settings.StripPosition is CurlingStripPosition.Bottom or CurlingStripPosition.Top;
            var isReversed = SlideShotPhysics.ResolveIsReversed(settings.SlideDirection, record.DropToken, isReversedOverride);

            var normalizedTarget = SlideShotPhysics.ResolveNormalizedTarget(settings.SlideDirection, targetDistancePercent, isReversed);

            var defaultTeePos = SlideShotPhysics.DefaultTeePosition(isReversed);
            var startPos = startAxisPosOverride.HasValue
                ? Math.Clamp(startAxisPosOverride.Value, 0.02, 0.98)
                : defaultTeePos;
            record.StartAxisPosition = startPos;

            // Aim automatically toward the pin from current ball lie
            var distToPin = Math.Abs(normalizedTarget - startPos);
            var directionSign = startAxisPosOverride.HasValue && distToPin > 0.001
                ? (normalizedTarget >= startPos ? 1.0 : -1.0)
                : (isReversed ? -1.0 : 1.0);

            // Determine club
            var club = ResolveClub(strokeNumber, distToPin);

            double travelDist;
            double v0;
            if (club == "Driver")
            {
                var baseV0 = PowerToInitialSpeed(effectivePower, dragMultiplier);
                v0 = baseV0 * Math.Sqrt(Math.Max(0.01, settings.DriverPowerMultiplier));
                travelDist = CalculateShotDistance(v0, dragMultiplier);
            }
            else if (club == "Wedge")
            {
                // Full wedge chip travels up to 0.60 track length (60 yards) scaled by WedgePowerMultiplier
                travelDist = (effectivePower / 100.0) * 0.60 * settings.WedgePowerMultiplier;
                v0 = Math.Sqrt(2.0 * decel * travelDist);
            }
            else
            {
                // Smooth putt across green up to 0.25 track length (25 yards) scaled by PutterPowerMultiplier
                travelDist = (effectivePower / 100.0) * 0.25 * settings.PutterPowerMultiplier;
                v0 = Math.Sqrt(2.0 * decel * travelDist);
            }

            var uncapturedRestPos = Math.Clamp(startPos + directionSign * travelDist, 0.02, 0.98);
            var restPos = uncapturedRestPos;
            var isLipOut = false;

            // Check if the ball reaches or crosses the cup along its roll path
            var startDeltaFromPin = (startPos - normalizedTarget) * directionSign; // negative when approaching pin
            var endDeltaFromPin = (uncapturedRestPos - normalizedTarget) * directionSign;

            var maxCaptureSpeed = BaseMaxCaptureSpeed * Math.Clamp(settings.CupCaptureSpeedMultiplier, 0.4, 2.5);

            // Does the ball stop inside the cup naturally, or roll across the cup?
            if (Math.Abs(uncapturedRestPos - normalizedTarget) <= CupRadius)
            {
                // Stopped right inside the cup capture zone
                restPos = normalizedTarget;
            }
            else if (startDeltaFromPin < -CupRadius && endDeltaFromPin > CupRadius)
            {
                // Ball rolled across the cup! Check its rolling speed as it passed over the pin:
                // Remaining distance past the pin is (endDeltaFromPin), so speed at pin = sqrt(2 * decel * overshootDist)
                var overshootDist = Math.Abs(uncapturedRestPos - normalizedTarget);
                var speedAtCup = Math.Sqrt(2.0 * decel * overshootDist);

                // Only ground-rolling balls can be captured or lip out (chip carry must have landed before or at the cup)
                var carryDist = club != "Putter" ? travelDist * 0.60 : 0.0;
                var distToCup = Math.Abs(normalizedTarget - startPos);
                var isRollingAtCup = carryDist <= distToCup + CupRadius;

                if (isRollingAtCup)
                {
                    if (speedAtCup <= maxCaptureSpeed)
                    {
                        // Captured by the cup!
                        restPos = normalizedTarget;
                    }
                    else
                    {
                        // Lip-out! Ball catches the cup rim, loses momentum, and deflects just past the lip
                        isLipOut = true;
                        var excessRatio = Math.Clamp((speedAtCup - maxCaptureSpeed) / 0.35, 0.0, 1.0);
                        var lipOutOffset = CupRadius * (1.35 + excessRatio * 1.45);
                        restPos = Math.Clamp(normalizedTarget + directionSign * lipOutOffset, 0.02, 0.98);
                    }
                }
            }

            record.IsLipOut = isLipOut;

            var actualTravelDist = Math.Abs(restPos - startPos);
            var durationSec = Math.Clamp(v0 / decel, 1.1, 4.2);
            if (strokeNumber > 1 && actualTravelDist < 0.25)
            {
                durationSec = Math.Clamp(durationSec * 0.65, 0.85, 2.5);
            }

            var totalTicks = (int)Math.Max(36, Math.Round(durationSec * TickRateHz));
            record.TotalTicks = totalTicks;

            var lateralOffset = 0.50;
            if (condition == GolfGreenCondition.CrossBreeze)
            {
                var r = rng ?? Random.Shared;
                var breezeDir = r.Next(0, 2) == 0 ? -1.0 : 1.0;
                lateralOffset = Math.Clamp(0.50 + breezeDir * 0.16 * (effectivePower / 100.0), 0.16, 0.84);
            }
            else if (isLipOut)
            {
                // Slight lateral rim deflection on lip-out
                var r = rng ?? Random.Shared;
                var rimDir = r.Next(0, 2) == 0 ? -1.0 : 1.0;
                lateralOffset = Math.Clamp(0.50 + rimDir * 0.06, 0.20, 0.80);
            }

            // Build 2.5D keyframes (parabolic flight arc + bounce + ground roll for Driver/Wedge shots; pure ground roll for Putter)
            var isAirborneShot = club != "Putter";
            if (isAirborneShot && actualTravelDist > 0.04)
            {
                var bounce1Tick = Math.Max(12, (int)Math.Round(totalTicks * 0.46));
                var apex1Tick = Math.Max(6, bounce1Tick / 2);
                var bounce2Tick = Math.Max(bounce1Tick + 6, (int)Math.Round(totalTicks * 0.66));
                var apex2Tick = (bounce1Tick + bounce2Tick) / 2;

                var posApex1 = startPos + directionSign * (actualTravelDist * 0.28);
                var posBounce1 = startPos + directionSign * (actualTravelDist * 0.55);
                var posApex2 = startPos + directionSign * (actualTravelDist * 0.65);
                var posBounce2 = startPos + directionSign * (actualTravelDist * 0.74);

                var peakHeight1 = Math.Round(Math.Clamp(0.32 * (effectivePower / 100.0), 0.08, 0.36), 4);
                var peakHeight2 = Math.Round(peakHeight1 * 0.28, 4);

                var startLateral = 0.50;
                var midLateral = (startLateral + lateralOffset) * 0.5;

                record.Keyframes =
                [
                    SlideShotPhysics.CreatePoint(0, startPos, startLateral, 0.0, v0 * directionSign, 0.0, isHorizontal),
                    SlideShotPhysics.CreatePoint(apex1Tick, posApex1, midLateral, peakHeight1, v0 * 0.82 * directionSign, 0.0, isHorizontal),
                    SlideShotPhysics.CreatePoint(bounce1Tick, posBounce1, lateralOffset, 0.0, v0 * 0.58 * directionSign, -peakHeight1 * 2.0, isHorizontal),
                    SlideShotPhysics.CreatePoint(apex2Tick, posApex2, lateralOffset, peakHeight2, v0 * 0.44 * directionSign, 0.0, isHorizontal),
                    SlideShotPhysics.CreatePoint(bounce2Tick, posBounce2, lateralOffset, 0.0, v0 * 0.32 * directionSign, -peakHeight2 * 2.0, isHorizontal),
                    SlideShotPhysics.CreatePoint(totalTicks, restPos, lateralOffset, 0.0, 0.0, 0.0, isHorizontal)
                ];
            }
            else
            {
                // Smooth ground putt
                record.Keyframes =
                [
                    SlideShotPhysics.CreatePoint(0, startPos, 0.50, 0.0, v0 * directionSign, 0.0, isHorizontal),
                    SlideShotPhysics.CreatePoint(totalTicks, restPos, lateralOffset, 0.0, 0.0, 0.0, isHorizontal)
                ];
            }

            record.LandingX = isHorizontal ? restPos : lateralOffset;
        }
    }
}

