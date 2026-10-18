using PenguinTwitchBot.Bot.Commands.Games.Common;

namespace PenguinTwitchBot.Bot.Commands.Games.Parachute
{
    public static class ParachuteSimulationEngine
    {
        public const double TickRateHz = 60.0;
        public const double Dt = 1.0 / TickRateHz;
        public const double ParachuteRadius = 0.036;
        public const double CanopyRadius = 0.042;
        public const double BodyRadius = 0.026;
        public const double CanopyYOffset = 0.045;
        public const double TerminalFreeFallVy = 0.98;
        public const double FreeFallGravity = 2.4;
        public const double MaxVx = 0.55;
        public const double MinVy = 0.04;
        public const double MaxVy = 0.45;
        public const double ParachuteDeployY = 0.08;

        public static ParachuteTrajectoryPointDto GetParachutePositionAtTick(PendingDropRecord record, int tick)
        {
            if (record.Keyframes.Count > 0)
            {
                var nearest = record.Keyframes.OrderBy(k => Math.Abs(k.Tick - tick)).First();
                return nearest;
            }

            var t = tick * Dt;
            var x = Math.Clamp(record.StartX + record.InitialVx * t, 0.06, 0.94);
            var y = Math.Clamp(record.StartY + record.InitialVy * t, record.StartY, record.LandingY);
            return new ParachuteTrajectoryPointDto { Tick = tick, X = x, Y = y, Vx = record.InitialVx, Vy = record.InitialVy };
        }

        /// <summary>
        /// Calculates baseline initial trajectory and uncollided landing for a Parachute drop.
        /// Live mid-air collisions (droplets, player bounces, rockets) are simulated on the client canvas,
        /// which reports the final landing coordinates with the secret key for authoritative scoring.
        /// </summary>
        public static void SimulateParachuteTrajectory(
            PendingDropRecord record,
            int elapsedTicksAtEvent = 0,
            List<DropletInstanceDto>? droplets = null,
            List<RocketInstanceDto>? rockets = null,
            double canopyKillChance = 1.0,
            double rocketKnockbackMultiplier = 1.0,
            CrashLandingScoreMode crashLandingScoreMode = CrashLandingScoreMode.PreserveScore,
            bool directHitKillsParachute = true)
        {
            var durationSec = record.TotalTicks > 0 ? record.TotalTicks * Dt : 6.0;
            var uncollidedX = record.StartX + record.InitialVx * durationSec;
            var keyframes = new List<ParachuteTrajectoryPointDto>();

            keyframes.Add(new ParachuteTrajectoryPointDto
            {
                Tick = 0,
                X = Math.Round(record.StartX, 4),
                Y = Math.Round(record.StartY, 4),
                Vx = Math.Round(record.InitialVx, 4),
                Vy = Math.Round(record.InitialVy, 4)
            });

            // Clean bounce off left/right screen edges if baseline glide trajectory crosses boundary
            if (uncollidedX < 0.05)
            {
                var bounceTick = (int)Math.Clamp(Math.Round(((0.05 - record.StartX) / (record.InitialVx != 0 ? record.InitialVx : -0.01)) * TickRateHz), 1, Math.Max(2, record.TotalTicks - 1));
                var bounceY = Math.Clamp(record.StartY + record.InitialVy * (bounceTick * Dt), record.StartY, record.LandingY);
                keyframes.Add(new ParachuteTrajectoryPointDto
                {
                    Tick = bounceTick,
                    X = 0.05,
                    Y = Math.Round(bounceY, 4),
                    Vx = Math.Round(-record.InitialVx, 4),
                    Vy = Math.Round(record.InitialVy, 4)
                });
                var remainingSec = Math.Max(0.0, (record.TotalTicks - bounceTick) * Dt);
                var reboundVx = Math.Abs(record.InitialVx) * 0.85 * 0.58;
                uncollidedX = 0.05 + reboundVx * remainingSec;
            }
            else if (uncollidedX > 0.95)
            {
                var bounceTick = (int)Math.Clamp(Math.Round(((0.95 - record.StartX) / (record.InitialVx != 0 ? record.InitialVx : 0.01)) * TickRateHz), 1, Math.Max(2, record.TotalTicks - 1));
                var bounceY = Math.Clamp(record.StartY + record.InitialVy * (bounceTick * Dt), record.StartY, record.LandingY);
                keyframes.Add(new ParachuteTrajectoryPointDto
                {
                    Tick = bounceTick,
                    X = 0.95,
                    Y = Math.Round(bounceY, 4),
                    Vx = Math.Round(-record.InitialVx, 4),
                    Vy = Math.Round(record.InitialVy, 4)
                });
                var remainingSec = Math.Max(0.0, (record.TotalTicks - bounceTick) * Dt);
                var reboundVx = Math.Abs(record.InitialVx) * 0.85 * 0.58;
                uncollidedX = 0.95 - reboundVx * remainingSec;
            }

            record.LandingX = Math.Round(Math.Clamp(uncollidedX, 0.05, 0.95), 4);
            record.CrashLandingScoreMode = crashLandingScoreMode;

            // Handle direct rocket canopy hit if simulated
            if (rockets != null && rockets.Count > 0)
            {
                if (canopyKillChance > 0 && directHitKillsParachute)
                {
                    record.ParachuteDestroyed = true;
                    record.RocketHitCount = rockets.Count;
                    record.FreeFallStartTick = Math.Max(0, elapsedTicksAtEvent);
                    record.Collisions.Add(new ParachuteCollisionEventDto
                    {
                        Tick = elapsedTicksAtEvent,
                        CollisionType = "rocket_canopy"
                    });
                    keyframes.Add(new ParachuteTrajectoryPointDto
                    {
                        Tick = record.TotalTicks,
                        X = record.LandingX,
                        Y = record.LandingY,
                        Vx = Math.Round(record.InitialVx, 4),
                        Vy = TerminalFreeFallVy
                    });
                }
                else
                {
                    record.RocketHitCount = rockets.Count;
                    record.Collisions.Add(new ParachuteCollisionEventDto
                    {
                        Tick = elapsedTicksAtEvent,
                        CollisionType = "rocket_body"
                    });
                }
            }

            keyframes.Add(new ParachuteTrajectoryPointDto
            {
                Tick = record.TotalTicks,
                X = record.LandingX,
                Y = record.LandingY,
                Vx = Math.Round(record.InitialVx, 4),
                Vy = record.ParachuteDestroyed ? TerminalFreeFallVy : Math.Round(record.InitialVy, 4)
            });

            record.Keyframes = keyframes;

            (record.Score, record.IsBullseye, record.IsWin, record.IsHit) =
                CalculateLandingScore(record.LandingX, record.TargetCenterX, record.TargetHalfWidth, record.ParachuteDestroyed, crashLandingScoreMode);
        }

        /// <summary>
        /// Evaluates landing accuracy against the target platform on a 0 - 100 scale.
        /// 0 = missed the platform
        /// 1 = outer edge of the platform
        /// 100 = dead center of the platform (bullseye)
        /// Configurable crash landing score mode handles destroyed parachute outcomes.
        /// </summary>
        public static (int Score, bool IsBullseye, bool IsWin, bool IsHit) CalculateLandingScore(
            double landingX,
            double targetCenterX,
            double targetHalfWidth,
            bool parachuteDestroyed = false,
            CrashLandingScoreMode scoreMode = CrashLandingScoreMode.PreserveScore)
        {
            var distanceFromCenter = Math.Round(Math.Abs(landingX - targetCenterX), 4);
            var isHit = distanceFromCenter <= targetHalfWidth;

            if (isHit)
            {
                var ratio = Math.Clamp(distanceFromCenter / Math.Max(0.0001, targetHalfWidth), 0.0, 1.0);
                var score = Math.Clamp((int)Math.Round(100.0 - (ratio * 99.0), MidpointRounding.AwayFromZero), 1, 100);
                var isBullseye = score == 100;

                if (parachuteDestroyed)
                {
                    switch (scoreMode)
                    {
                        case CrashLandingScoreMode.ZeroScore:
                            return (0, false, false, true);
                        case CrashLandingScoreMode.HalfScore:
                            var halfScore = Math.Max(1, (int)Math.Round(score * 0.5, MidpointRounding.AwayFromZero));
                            return (halfScore, false, true, true);
                        case CrashLandingScoreMode.PreserveScore:
                        default:
                            return (score, isBullseye, true, true);
                    }
                }

                return (score, isBullseye, true, true);
            }

            return (0, false, false, false);
        }
    }
}
