using System.Security.Cryptography;
using PenguinTwitchBot.Bot.Commands.Games.Common;

namespace PenguinTwitchBot.Bot.Commands.Games.Parachute
{
    public static class RocketTrajectoryGenerator
    {
        public const double TickRateHz = 60.0;
        public const double Dt = 1.0 / TickRateHz;
        public const double CanopyYOffset = 0.045; // Canopy is centered slightly above character body
        public const double RocketRadius = 0.024;

        private static readonly string[] FireworkColors =
        [
            "#FF3366", // Neon Pink
            "#FF9900", // Gold Orange
            "#00F0FF", // Neon Cyan
            "#7000FF", // Royal Purple
            "#39FF14", // Electric Lime
            "#FFFF00", // Bright Yellow
            "#FF3333"  // Crimson Red
        ];

        public static string PickRandomColor() =>
            FireworkColors[RandomNumberGenerator.GetInt32(0, FireworkColors.Length)];

        public static List<RocketInstanceDto> GenerateRockets(
            RocketFireRequest request,
            PendingDropRecord? targetDrop,
            DateTime now) =>
            GenerateRockets(request, targetDrop, null, now);

        public static List<RocketInstanceDto> GenerateRockets(
            RocketFireRequest request,
            PendingDropRecord? targetDrop,
            IReadOnlyList<PendingDropRecord>? candidateDrops,
            DateTime now)
        {
            var count = Math.Clamp(request.Count, 1, 10);
            var pattern = string.IsNullOrWhiteSpace(request.Pattern) ? "Targeted" : request.Pattern.Trim();
            var rockets = new List<RocketInstanceDto>(count);

            for (var i = 0; i < count; i++)
            {
                var spawnTickOffset = i * 8; // Stagger barrage/multi-rocket launches by ~130ms each

                var rocketTarget = targetDrop;
                if (rocketTarget == null &&
                    pattern.Equals("Targeted", StringComparison.OrdinalIgnoreCase) &&
                    candidateDrops != null &&
                    candidateDrops.Count > 0)
                {
                    rocketTarget = candidateDrops[RandomNumberGenerator.GetInt32(0, candidateDrops.Count)];
                }

                var rocket = CreateRocket(
                    pattern,
                    i,
                    count,
                    spawnTickOffset,
                    rocketTarget,
                    now,
                    request.KnockbackMultiplier ?? 1.0,
                    request.CanopyKillChance ?? 1.0,
                    request.DirectHitKillsParachute ?? true);
                rockets.Add(rocket);
            }

            return rockets;
        }

        private static RocketInstanceDto CreateRocket(
            string pattern,
            int index,
            int totalCount,
            int spawnTickOffset,
            PendingDropRecord? targetDrop,
            DateTime now,
            double knockbackMultiplier,
            double canopyKillChance,
            bool directHitKillsParachute = true)
        {
            var id = $"rocket_{DateTime.UtcNow.Ticks}_{index}_{RandomNumberGenerator.GetInt32(1000, 9999)}";
            var color = PickRandomColor();
            var startY = 1.02; // Launch from below ground / bottom of screen
            var accelY = 0.04; // Slight upward drag deceleration as rocket climbs

            if (pattern.Equals("Targeted", StringComparison.OrdinalIgnoreCase))
            {
                var groundSpread = (index - ((totalCount - 1) / 2.0)) * 0.04;
                var startX = targetDrop != null
                    ? Math.Clamp(targetDrop.LandingX + groundSpread, 0.08, 0.92)
                    : (totalCount > 1 ? Math.Clamp(0.18 + (index * (0.64 / (totalCount - 1))) + DropGameMath.RandomDouble(-0.02, 0.02), 0.12, 0.88) : 0.50);

                var targetApexY = DropGameMath.RandomDouble(0.10, 0.25);
                var flightTicks = RandomNumberGenerator.GetInt32(72, 92);
                var flightDuration = flightTicks * Dt;
                var vy = ((targetApexY - startY) - (0.5 * accelY * flightDuration * flightDuration)) / flightDuration;

                return new RocketInstanceDto
                {
                    Id = id,
                    SpawnTickOffset = spawnTickOffset,
                    StartX = Math.Round(startX, 4),
                    StartY = startY,
                    TargetX = Math.Round(startX, 4),
                    TargetY = Math.Round(targetApexY, 4),
                    Vx = 0.0,
                    Vy = Math.Round(vy, 4),
                    AccelerationY = accelY,
                    FuseTicks = flightTicks,
                    Color = color,
                    TrailType = "sparks",
                    TargetDropToken = targetDrop?.DropToken,
                    Pattern = "Targeted",
                    IsTargeted = true,
                    KnockbackMultiplier = knockbackMultiplier,
                    CanopyKillChance = canopyKillChance,
                    DirectHitKillsParachute = directHitKillsParachute
                };
            }

            if (pattern.Equals("Barrage", StringComparison.OrdinalIgnoreCase))
            {
                // Fan spread across the play area
                var step = 0.70 / Math.Max(1, totalCount);
                var startX = 0.15 + (index * step) + DropGameMath.RandomDouble(-0.02, 0.02);
                startX = Math.Clamp(startX, 0.08, 0.92);

                // Angle slightly towards screen center
                var centerOffset = 0.50 - startX;
                var vx = centerOffset * DropGameMath.RandomDouble(0.15, 0.30);

                // High sky apex detonation (0.06 to 0.18 from top of canvas)
                var targetApexY = DropGameMath.RandomDouble(0.06, 0.18);
                var flightTicks = RandomNumberGenerator.GetInt32(72, 92);
                var flightDuration = flightTicks * Dt;
                var vy = ((targetApexY - startY) - (0.5 * accelY * flightDuration * flightDuration)) / flightDuration;
                var targetX = Math.Clamp(startX + vx * flightDuration, 0.05, 0.95);

                return new RocketInstanceDto
                {
                    Id = id,
                    SpawnTickOffset = spawnTickOffset,
                    StartX = Math.Round(startX, 4),
                    StartY = startY,
                    TargetX = Math.Round(targetX, 4),
                    TargetY = Math.Round(targetApexY, 4),
                    Vx = Math.Round(vx, 4),
                    Vy = Math.Round(vy, 4),
                    AccelerationY = accelY,
                    FuseTicks = flightTicks,
                    Color = color,
                    TrailType = "sparks",
                    TargetDropToken = null,
                    Pattern = "Barrage",
                    IsTargeted = false,
                    KnockbackMultiplier = knockbackMultiplier,
                    CanopyKillChance = canopyKillChance,
                    DirectHitKillsParachute = directHitKillsParachute
                };
            }

            // Default: Random trajectory soaring high into the upper sky
            {
                var startX = DropGameMath.RandomDouble(0.12, 0.88);
                var vx = DropGameMath.RandomDouble(-0.20, 0.20);
                var targetApexY = DropGameMath.RandomDouble(0.06, 0.18);
                var flightTicks = RandomNumberGenerator.GetInt32(72, 92);
                var flightDuration = flightTicks * Dt;
                var vy = ((targetApexY - startY) - (0.5 * accelY * flightDuration * flightDuration)) / flightDuration;
                var targetX = Math.Clamp(startX + vx * flightDuration, 0.05, 0.95);

                return new RocketInstanceDto
                {
                    Id = id,
                    SpawnTickOffset = spawnTickOffset,
                    StartX = Math.Round(startX, 4),
                    StartY = startY,
                    TargetX = Math.Round(targetX, 4),
                    TargetY = Math.Round(targetApexY, 4),
                    Vx = Math.Round(vx, 4),
                    Vy = Math.Round(vy, 4),
                    AccelerationY = accelY,
                    FuseTicks = flightTicks,
                    Color = color,
                    TrailType = "sparks",
                    TargetDropToken = null,
                    Pattern = "Random",
                    IsTargeted = false,
                    KnockbackMultiplier = knockbackMultiplier,
                    CanopyKillChance = canopyKillChance,
                    DirectHitKillsParachute = directHitKillsParachute
                };
            }
        }
    }
}

