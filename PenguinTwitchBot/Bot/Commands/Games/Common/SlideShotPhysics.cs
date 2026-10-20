namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    /// <summary>
    /// Shared physics/layout helpers for "launch along a strip toward a target" games (Curling, Golf, ...).
    /// Game-specific engines keep their own tuning constants, scoring and keyframe shape; everything
    /// that was identical between them lives here.
    /// </summary>
    public static class SlideShotPhysics
    {
        public const double TickRateHz = 60.0;

        /// <summary>Baseline friction deceleration on a neutral surface (drag multiplier 1.0).</summary>
        public const double BaseDecel = 0.22;

        /// <summary>Maximum initial launch speed (power 100).</summary>
        public const double MaxLaunchSpeed = 0.76;

        /// <summary>Applies streamer variance (+/- variancePercent) to a raw 1-100 power and clamps the result.</summary>
        public static double CalculateEffectivePower(double rawPower, double variancePercent, Random? rng = null)
        {
            if (!double.IsFinite(rawPower))
            {
                rawPower = 75.0;
            }
            var clampedRaw = Math.Clamp(rawPower, 1.0, 100.0);
            if (variancePercent <= 0.0) return clampedRaw;

            var r = rng ?? Random.Shared;
            var delta = (r.NextDouble() * 2.0 - 1.0) * variancePercent;
            return Math.Clamp(clampedRaw + delta, 1.0, 100.0);
        }

        /// <summary>Converts an effective power (1-100) into initial launch speed.</summary>
        public static double PowerToInitialSpeed(double effectivePower)
        {
            var powerFraction = Math.Clamp(effectivePower / 100.0, 0.01, 1.0);
            return Math.Round(powerFraction * MaxLaunchSpeed, 4);
        }

        /// <summary>Total travel distance from launch speed and surface drag: d = v^2 / (2a).</summary>
        public static double CalculateTravelDistance(double initialSpeed, double dragMultiplier)
        {
            var decel = Math.Max(0.01, BaseDecel * dragMultiplier);
            return (initialSpeed * initialSpeed) / (2.0 * decel);
        }

        /// <summary>Resolves whether the shot travels in the reverse axis direction (right-to-left / bottom-to-top).</summary>
        public static bool ResolveIsReversed(CurlingSlideDirection slideDirection, string dropToken, bool? isReversedOverride)
        {
            if (isReversedOverride.HasValue) return isReversedOverride.Value;
            if (slideDirection == CurlingSlideDirection.Alternating)
            {
                return (dropToken.GetHashCode() & 1) == 1;
            }
            return slideDirection is CurlingSlideDirection.RightToLeft or CurlingSlideDirection.BottomToTop;
        }

        /// <summary>Target position normalized along the slide axis, mirrored for reversed shots.</summary>
        public static double ResolveNormalizedTarget(CurlingSlideDirection slideDirection, double targetDistancePercent, bool isReversed)
        {
            var normalizedTarget = slideDirection == CurlingSlideDirection.CenterTarget
                ? 0.50
                : Math.Clamp(targetDistancePercent / 100.0, 0.50, 0.95);
            return isReversed ? 1.0 - normalizedTarget : normalizedTarget;
        }

        /// <summary>Default tee/launch position along the slide axis.</summary>
        public static double DefaultTeePosition(bool isReversed) => isReversed ? 0.95 : 0.05;

        /// <summary>Builds a keyframe, mapping axis/lateral values onto X/Y depending on strip orientation.</summary>
        public static ParachuteTrajectoryPointDto CreatePoint(
            int tick,
            double axisPos,
            double lateralPos,
            double heightZ,
            double vAxis,
            double vz,
            bool isHorizontal)
        {
            return new ParachuteTrajectoryPointDto
            {
                Tick = tick,
                X = isHorizontal ? axisPos : lateralPos,
                Y = isHorizontal ? lateralPos : axisPos,
                Z = heightZ,
                Vx = isHorizontal ? vAxis : 0.0,
                Vy = isHorizontal ? 0.0 : vAxis,
                Vz = vz
            };
        }
    }
}

