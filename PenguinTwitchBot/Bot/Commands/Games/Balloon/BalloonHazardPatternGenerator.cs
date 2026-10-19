using System.Security.Cryptography;
using PenguinTwitchBot.Bot.Commands.Games.Common;

namespace PenguinTwitchBot.Bot.Commands.Games.Balloon
{
    public static class BalloonHazardPatternGenerator
    {
        public const double TickRateHz = 60.0;
        public const double Dt = 1.0 / TickRateHz;

        public static (string Label, double Scale, double Radius, double Mass) ResolveHazardPhysics(string hazardType, string sizePreset)
        {
            var baseScale = sizePreset.ToLowerInvariant() switch
            {
                "small" => 0.60,
                "large" => 1.50,
                "giant" => 2.00,
                _ => 1.00
            };

            return hazardType.ToLowerInvariant() switch
            {
                "heavypush" => ("Anvil", baseScale * 1.3, 0.038 * baseScale, 9.5 * baseScale),
                "dartpop" => ("Dart", baseScale * 0.9, 0.022 * baseScale, 1.2 * baseScale),
                "windgust" => ("Wind", baseScale * 1.5, 0.045 * baseScale, 0.5 * baseScale),
                _ => ("Droplet", baseScale, 0.028 * baseScale, 3.0 * baseScale) // Nudge
            };
        }

        public static (string Label, double Scale) ResolveHazardSpeed(string preset)
        {
            var normalized = preset?.Trim() ?? "Normal";
            if (normalized.Equals("Random", StringComparison.OrdinalIgnoreCase))
            {
                string[] choices = ["Slow", "Normal", "Fast", "Hyper"];
                normalized = choices[RandomNumberGenerator.GetInt32(0, choices.Length)];
            }

            return normalized.ToLowerInvariant() switch
            {
                "slow" => ("Slow", 0.65),
                "fast" => ("Fast", 1.60),
                "hyper" => ("Hyper", 2.20),
                _ => ("Normal", 1.0)
            };
        }

        public static (double StartX, double StartY, double Vx, double Vy, double Gravity) CalculateTrajectory(
            string pattern,
            string hazardType,
            int index,
            double speedScale,
            PendingDropRecord? targetDrop,
            DateTime now,
            int spawnTickOffset)
        {
            if (pattern.Equals("Targeted", StringComparison.OrdinalIgnoreCase) && targetDrop != null)
            {
                var elapsedSeconds = Math.Max(0.0, (now - targetDrop.CreatedAtUtc).TotalSeconds);
                var maxTick = Math.Max(0, targetDrop.TotalTicks - 10);
                var currentTick = Math.Clamp((int)Math.Round(elapsedSeconds * TickRateHz) + spawnTickOffset, 0, maxTick);
                var targetPoint = BalloonAscentSimulationEngine.GetBalloonPositionAtTick(targetDrop, Math.Min(Math.Max(0, targetDrop.TotalTicks - 5), currentTick + 20));

                var fromLeft = (index % 2 == 0) ? (targetPoint.X > 0.35) : (targetPoint.X < 0.65);
                var startX = fromLeft ? Math.Max(0.04, targetPoint.X - 0.25) : Math.Min(0.96, targetPoint.X + 0.25);
                var startY = Math.Clamp(targetPoint.Y - 0.25, 0.02, 0.70);

                var interceptTime = 20.0 * Dt / Math.Max(0.6, speedScale);
                var vx = (targetPoint.X - startX) / interceptTime;
                var vy = (targetPoint.Y - startY) / interceptTime;
                var gravity = 0.08 * speedScale;
                return (startX, startY, vx, vy, gravity);
            }

            if (pattern.Equals("SideWind", StringComparison.OrdinalIgnoreCase) || hazardType.Equals("WindGust", StringComparison.OrdinalIgnoreCase))
            {
                var fromLeft = index % 2 == 0;
                var startX = fromLeft ? 0.02 : 0.98;
                var startY = DropGameMath.RandomDouble(0.15, 0.75);
                var vx = (fromLeft ? 1.0 : -1.0) * DropGameMath.RandomDouble(0.35, 0.55) * speedScale;
                var vy = DropGameMath.RandomDouble(-0.04, 0.08) * speedScale;
                var gravity = 0.01;
                return (startX, startY, vx, vy, gravity);
            }

            // Default TopRain / Fountain
            var rx = DropGameMath.RandomDouble(0.08, 0.92);
            var rStartY = 0.02;
            var rVx = DropGameMath.RandomDouble(-0.08, 0.08) * speedScale;
            var rVy = DropGameMath.RandomDouble(0.12, 0.24) * speedScale;
            var rGravity = 0.14 * speedScale;
            return (rx, rStartY, rVx, rVy, rGravity);
        }
    }
}

