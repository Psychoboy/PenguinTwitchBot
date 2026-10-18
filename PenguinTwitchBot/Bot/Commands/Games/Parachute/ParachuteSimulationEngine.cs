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
        /// Deterministic 60Hz fixed-timestep simulation for Parachute Drop + mid-air Droplet and Rocket collisions.
        /// Preserves existing trajectory up to elapsedTicksAtEvent and simulates impulses/canopy destruction forward.
        /// </summary>
        public static void SimulateParachuteTrajectory(
            PendingDropRecord record,
            int elapsedTicksAtEvent,
            List<DropletInstanceDto>? droplets = null,
            List<RocketInstanceDto>? rockets = null,
            double canopyKillChance = 1.0,
            double rocketKnockbackMultiplier = 1.0,
            CrashLandingScoreMode crashLandingScoreMode = CrashLandingScoreMode.PreserveScore,
            bool directHitKillsParachute = true)
        {
            droplets ??= [];
            rockets ??= [];

            double x = record.StartX;
            double y = record.StartY;
            double vx = record.InitialVx;
            double vy = record.InitialVy;

            var existingCollisions = elapsedTicksAtEvent > 0
                ? record.Collisions.Where(c => c.Tick < elapsedTicksAtEvent).ToList()
                : [];

            var startTick = 0;
            if (elapsedTicksAtEvent > 0 && record.Keyframes.Count > 0)
            {
                var priorPoint = record.Keyframes
                    .Where(k => k.Tick <= elapsedTicksAtEvent)
                    .OrderByDescending(k => k.Tick)
                    .FirstOrDefault();

                if (priorPoint != null)
                {
                    startTick = priorPoint.Tick;
                    x = priorPoint.X;
                    y = priorPoint.Y;
                    vx = priorPoint.Vx;
                    vy = priorPoint.Vy;
                }
                else
                {
                    startTick = Math.Max(0, elapsedTicksAtEvent);
                }
            }

            var keyframes = startTick > 0
                ? record.Keyframes.Where(k => k.Tick < startTick).ToList()
                : [];

            var hitDropletIds = new HashSet<string>(StringComparer.Ordinal);
            var hitRocketIds = new HashSet<string>(StringComparer.Ordinal);
            var bounciness = Math.Clamp(record.Bounciness > 0 ? record.Bounciness : 0.85, 0.1, 5.0);
            var parachuteMass = Math.Clamp(record.Mass > 0 ? record.Mass : 1.0, 0.1, 10.0);
            var maxAllowedTicks = Math.Max(record.TotalTicks, 30) + 180; // Bounded cap (~3 seconds extra if vertical speed was dampened)
            var finalTick = startTick;
            var bouncedOffTargetRim = false;

            for (var tick = startTick; tick <= maxAllowedTicks; tick++)
            {
                finalTick = tick;

                // Check collisions with any active droplets at this tick
                if (droplets.Count > 0 && y < record.LandingY - 0.02)
                {
                    var localTick = tick - elapsedTicksAtEvent;
                    foreach (var droplet in droplets)
                    {
                        if (localTick < droplet.SpawnTickOffset || hitDropletIds.Contains(droplet.Id))
                        {
                            continue;
                        }

                        var dtDroplet = (localTick - droplet.SpawnTickOffset) * Dt;
                        var dx = droplet.StartX + droplet.Vx * dtDroplet;
                        var dy = droplet.StartY + droplet.Vy * dtDroplet + 0.5 * droplet.Gravity * dtDroplet * dtDroplet;
                        var dvx = droplet.Vx;
                        var dvy = droplet.Vy + droplet.Gravity * dtDroplet;

                        var distX = x - dx;
                        var distY = y - dy;
                        var distSq = distX * distX + distY * distY;
                        var minDist = ParachuteRadius + droplet.Radius;

                        if (distSq <= minDist * minDist)
                        {
                            hitDropletIds.Add(droplet.Id);
                            var dist = Math.Sqrt(Math.Max(0.0001, distSq));
                            var nx = distX / dist;
                            var ny = distY / dist;

                            // Relative velocity and momentum impulse scaled by droplet mass, parachute mass, and bounciness
                            var relVx = dvx - vx;
                            var relVy = dvy - vy;
                            var massRatio = droplet.Mass / parachuteMass;
                            var restitutionScale = Math.Clamp(bounciness / 0.85, 0.2, 4.0);
                            var impulseMagnitude = 0.16 * massRatio * Math.Max(0.7, droplet.SpeedScale) * restitutionScale;

                            // Push horizontally in the direction of droplet travel + collision normal
                            var pushDirectionX = Math.Abs(dvx) > 0.04 ? Math.Sign(dvx) : (Math.Abs(nx) > 0.05 ? Math.Sign(nx) : 1.0);
                            var deltaVx = Math.Clamp((pushDirectionX * impulseMagnitude) + (relVx * 0.25 * Math.Min(5.0, massRatio) * restitutionScale), -0.65, 0.65);
                            var deltaVy = Math.Clamp((ny * impulseMagnitude * 0.75) + (relVy * 0.12), -1.20, 0.55);

                            vx = Math.Clamp(vx + deltaVx, -MaxVx, MaxVx);
                            vy = Math.Clamp(vy + deltaVy, -1.20, Math.Max(MaxVy, record.InitialVy * 1.15));

                            existingCollisions.Add(new ParachuteCollisionEventDto
                            {
                                Tick = tick,
                                DropletId = droplet.Id,
                                CollisionType = "droplet",
                                X = Math.Round(x, 4),
                                Y = Math.Round(y, 4),
                                DeltaVx = Math.Round(deltaVx, 4),
                                DeltaVy = Math.Round(deltaVy, 4),
                                SizeScale = droplet.SizeScale
                            });
                        }
                    }
                }

                // Check collisions with any active sky rockets at this tick
                if (rockets.Count > 0 && y < record.LandingY - 0.01)
                {
                    var localTick = tick - elapsedTicksAtEvent;
                    foreach (var rocket in rockets)
                    {
                        if (localTick < rocket.SpawnTickOffset ||
                            localTick > (rocket.SpawnTickOffset + rocket.FuseTicks) ||
                            hitRocketIds.Contains(rocket.Id))
                        {
                            continue;
                        }

                        var dtRocket = (localTick - rocket.SpawnTickOffset) * Dt;
                        var rx = rocket.StartX + rocket.Vx * dtRocket;
                        var ry = rocket.StartY + rocket.Vy * dtRocket + 0.5 * rocket.AccelerationY * dtRocket * dtRocket;

                        // 1. Direct Canopy Collision Check
                        var canopyY = y - CanopyYOffset;
                        var cDistX = x - rx;
                        var cDistY = canopyY - ry;
                        var cDistSq = cDistX * cDistX + cDistY * cDistY;
                        var minCanopyDist = CanopyRadius + RocketTrajectoryGenerator.RocketRadius;

                        if (cDistSq <= minCanopyDist * minCanopyDist)
                        {
                            hitRocketIds.Add(rocket.Id);
                            record.RocketHitCount++;

                            // Canopy direct hit: chance to destroy parachute
                            var roll = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 100);
                            if (directHitKillsParachute && roll < canopyKillChance * 100 && !record.ParachuteDestroyed)
                            {
                                record.ParachuteDestroyed = true;
                                record.FreeFallStartTick = tick;
                            }

                            var knockback = Math.Clamp(rocketKnockbackMultiplier, 0.1, 5.0);
                            var deltaVx = Math.Clamp(Math.Sign(rocket.Vx) * 0.18 * knockback, -0.40, 0.40);
                            var deltaVy = 0.08;

                            vx = Math.Clamp(vx + deltaVx, -MaxVx * 1.5, MaxVx * 1.5);
                            vy = Math.Clamp(vy + deltaVy, MinVy, Math.Max(MaxVy, record.InitialVy * 1.3));

                            existingCollisions.Add(new ParachuteCollisionEventDto
                            {
                                Tick = tick,
                                DropletId = "",
                                RocketId = rocket.Id,
                                CollisionType = "rocket_canopy",
                                X = Math.Round(x, 4),
                                Y = Math.Round(y, 4),
                                DeltaVx = Math.Round(deltaVx, 4),
                                DeltaVy = Math.Round(deltaVy, 4),
                                SizeScale = 1.2
                            });
                            continue;
                        }

                        // 2. Body / Blast Wave Collision Check
                        var bDistX = x - rx;
                        var bDistY = y - ry;
                        var bDistSq = bDistX * bDistX + bDistY * bDistY;
                        var minBodyDist = BodyRadius + RocketTrajectoryGenerator.RocketRadius;

                        if (bDistSq <= minBodyDist * minBodyDist)
                        {
                            hitRocketIds.Add(rocket.Id);
                            record.RocketHitCount++;

                            var dist = Math.Sqrt(Math.Max(0.0001, bDistSq));
                            var nx = bDistX / dist;
                            var ny = bDistY / dist;

                            var impulse = 0.28 * Math.Clamp(rocketKnockbackMultiplier, 0.1, 5.0) * (bounciness / 0.85) / parachuteMass;
                            var deltaVx = Math.Clamp(nx * impulse, -0.65, 0.65);
                            var deltaVy = Math.Clamp(ny * impulse * 0.55, -0.45, 0.45);

                            vx = Math.Clamp(vx + deltaVx, -MaxVx * 1.5, MaxVx * 1.5);
                            vy = Math.Clamp(vy + deltaVy, -1.20, Math.Max(MaxVy, record.InitialVy * 1.3));

                            existingCollisions.Add(new ParachuteCollisionEventDto
                            {
                                Tick = tick,
                                DropletId = "",
                                RocketId = rocket.Id,
                                CollisionType = "rocket_body",
                                X = Math.Round(x, 4),
                                Y = Math.Round(y, 4),
                                DeltaVx = Math.Round(deltaVx, 4),
                                DeltaVy = Math.Round(deltaVy, 4),
                                SizeScale = 1.2
                            });
                        }
                    }
                }

                if (tick % 6 == 0 || tick >= record.TotalTicks)
                {
                    keyframes.Add(new ParachuteTrajectoryPointDto
                    {
                        Tick = tick,
                        X = Math.Round(x, 4),
                        Y = Math.Round(y, 4),
                        Vx = Math.Round(vx, 4),
                        Vy = Math.Round(vy, 4)
                    });
                }

                if (y >= record.LandingY)
                {
                    break;
                }

                // Step physics: free-fall acceleration if parachute is destroyed, or steady glide with canopy
                if (record.ParachuteDestroyed)
                {
                    vy = Math.Min(TerminalFreeFallVy, vy + FreeFallGravity * Dt);
                    vx = Math.Clamp(vx * 0.994, -MaxVx * 1.5, MaxVx * 1.5);
                }
                else
                {
                    vx = Math.Clamp(vx * 0.998, -MaxVx, MaxVx);
                    if (vy < record.InitialVy)
                    {
                        // Ballistic upward arc decelerates under gravity back to terminal glide (matching parachute.js / Matter.js)
                        vy = Math.Min(record.InitialVy, vy + 0.38 * Dt);
                    }
                    else
                    {
                        vy = Math.Clamp(record.InitialVy + (vy - record.InitialVy) * 0.96, MinVy, Math.Max(MaxVy, record.InitialVy * 1.15)); // Canopy stabilizes vertical speed
                    }
                }

                x += vx * Dt;
                y += vy * Dt;
                if (y < 0.04)
                {
                    y = 0.04;
                    vy = Math.Max(0.0, vy);
                }

                // Interactive Target Rim / Wall Bounce (Pool / Cauldron / Leaf Pile side lip)
                if (!bouncedOffTargetRim && y >= record.LandingY - 0.038 && y < record.LandingY)
                {
                    var dx = x - record.TargetCenterX;
                    var dist = Math.Abs(dx);
                    if (dist >= record.TargetHalfWidth * 0.93 && dist <= record.TargetHalfWidth * 1.07)
                    {
                        bouncedOffTargetRim = true;
                        // Outer rim bounce: deflects outward or bounces over the lip into the basin
                        if (Math.Sign(vx) == Math.Sign(dx) && Math.Abs(vx) > 0.01)
                        {
                            vx = Math.Clamp(-vx * bounciness, -MaxVx, MaxVx); // Bounced off outer wall!
                        }
                        else
                        {
                            vx = Math.Clamp((dx > 0 ? -1 : 1) * Math.Max(0.04, Math.Abs(vx) * 0.95 * bounciness), -MaxVx, MaxVx); // Bounced over lip into the pool!
                        }
                        vy = Math.Max(MinVy, vy * Math.Clamp(0.65 * bounciness, 0.25, 1.0)); // Springy cushion pop

                        keyframes.Add(new ParachuteTrajectoryPointDto
                        {
                            Tick = tick,
                            X = Math.Round(x, 4),
                            Y = Math.Round(y, 4),
                            Vx = Math.Round(vx, 4),
                            Vy = Math.Round(vy, 4)
                        });
                    }
                }

                // Clean bounce off left/right screen margins
                var wallRestitution = Math.Clamp(bounciness * 1.05, 0.15, 2.5);
                if (x < 0.05)
                {
                    x = 0.05;
                    vx = Math.Min(MaxVx, Math.Abs(vx) * wallRestitution);
                }
                else if (x > 0.95)
                {
                    x = 0.95;
                    vx = -Math.Min(MaxVx, Math.Abs(vx) * wallRestitution);
                }
            }

            record.TotalTicks = finalTick;
            record.LandingX = Math.Round(Math.Clamp(x, 0.05, 0.95), 4);
            record.Collisions = existingCollisions;
            record.DropletHitCount = existingCollisions.Count(c => c.CollisionType == "droplet");
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

