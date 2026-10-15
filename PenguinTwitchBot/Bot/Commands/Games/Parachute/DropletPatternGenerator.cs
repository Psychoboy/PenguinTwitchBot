using System.Security.Cryptography;
using PenguinTwitchBot.Bot.Commands.Games.Common;

namespace PenguinTwitchBot.Bot.Commands.Games.Parachute
{
    public static class DropletPatternGenerator
    {
        public const double TickRateHz = 60.0;
        public const double Dt = 1.0 / TickRateHz;

        public static (string Label, double Scale, double Radius, double Mass) ResolveDropletSize(string preset)
        {
            var normalized = preset?.Trim() ?? "Medium";
            if (normalized.Equals("Random", StringComparison.OrdinalIgnoreCase))
            {
                // Weighted selection matching PixelPlush's [1, .5, .5, .5, .5, .5] distribution
                // ~17% Large/Giant, ~83% Small/Medium
                var roll = RandomNumberGenerator.GetInt32(0, 100);
                normalized = roll switch
                {
                    < 25 => "Small",
                    < 75 => "Medium",
                    < 92 => "Large",
                    _ => "Giant"
                };
            }

            return normalized.ToLowerInvariant() switch
            {
                "small" => ("Small", 0.60, 0.018, 0.50),
                "large" => ("Large", 1.50, 0.042, 1.80),
                "giant" => ("Giant", 2.00, 0.058, 2.80),
                _ => ("Medium", 1.00, 0.028, 1.00)
            };
        }

        public static (string Label, double Scale) ResolveDropletSpeed(string preset)
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
            int index,
            double speedScale,
            PendingDropRecord? targetDrop,
            DateTime now,
            int spawnTickOffset)
        {
            if (pattern.Equals("Targeted", StringComparison.OrdinalIgnoreCase) && targetDrop != null)
            {
                var elapsedSeconds = Math.Max(0.0, (now - targetDrop.CreatedAtUtc).TotalSeconds);
                var currentTick = Math.Clamp((int)Math.Round(elapsedSeconds * TickRateHz) + spawnTickOffset, 0, targetDrop.TotalTicks - 10);
                var targetPoint = ParachuteSimulationEngine.GetParachutePositionAtTick(targetDrop, Math.Min(targetDrop.TotalTicks - 5, currentTick + 24));

                var fromLeft = (index % 2 == 0) ? (targetPoint.X > 0.35) : (targetPoint.X < 0.65);
                var startX = fromLeft ? Math.Max(0.04, targetPoint.X - 0.28) : Math.Min(0.96, targetPoint.X + 0.28);
                var startY = Math.Max(0.04, targetPoint.Y - 0.22);

                var interceptTime = 24.0 * Dt / Math.Max(0.6, speedScale);
                var vx = (targetPoint.X - startX) / interceptTime;
                var vy = (targetPoint.Y - startY) / interceptTime;
                var gravity = 0.08 * speedScale;
                return (startX, startY, vx, vy, gravity);
            }

            if (pattern.Equals("SideWind", StringComparison.OrdinalIgnoreCase))
            {
                var fromLeft = index % 2 == 0;
                var startX = fromLeft ? 0.02 : 0.98;
                var startY = DropGameMath.RandomDouble(0.12, 0.62);
                var vx = (fromLeft ? 1.0 : -1.0) * DropGameMath.RandomDouble(0.32, 0.52) * speedScale;
                var vy = DropGameMath.RandomDouble(0.12, 0.26) * speedScale;
                var gravity = 0.14 * speedScale;
                return (startX, startY, vx, vy, gravity);
            }

            if (pattern.Equals("Fountain", StringComparison.OrdinalIgnoreCase))
            {
                var fromLeft = index % 2 == 0;
                var startX = fromLeft ? DropGameMath.RandomDouble(0.08, 0.25) : DropGameMath.RandomDouble(0.75, 0.92);
                var startY = 0.85;
                var vx = (fromLeft ? 1.0 : -1.0) * DropGameMath.RandomDouble(0.22, 0.40) * speedScale;
                var vy = -DropGameMath.RandomDouble(0.55, 0.78) * speedScale;
                var gravity = 0.52 * speedScale;
                return (startX, startY, vx, vy, gravity);
            }

            // Default TopRain (matching PixelPlush bouncing obstacles)
            {
                var startX = DropGameMath.RandomDouble(0.06, 0.94);
                var startY = -0.06;
                var vx = DropGameMath.RandomDouble(-0.35, 0.35) * speedScale;
                var vy = DropGameMath.RandomDouble(0.06, 0.20) * speedScale;
                var gravity = 0.32 * speedScale;
                return (startX, startY, vx, vy, gravity);
            }
        }
    }
}

