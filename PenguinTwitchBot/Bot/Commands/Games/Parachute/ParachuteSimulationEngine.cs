using PenguinTwitchBot.Bot.Commands.Games.Common;

namespace PenguinTwitchBot.Bot.Commands.Games.Parachute
{
    public static class ParachuteSimulationEngine
    {
        public const double TickRateHz = 60.0;
        public const double Dt = 1.0 / TickRateHz;
        public const double ParachuteRadius = 0.036;
        public const double MaxVx = 0.55;
        public const double MinVy = 0.04;
        public const double MaxVy = 0.45;

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
        /// Deterministic 60Hz fixed-timestep simulation for Parachute Drop + mid-air Droplet collisions.
        /// Preserves existing trajectory up to elapsedTicksAtDropletSpawn and simulates droplet impulses forward.
        /// </summary>
        public static void SimulateParachuteTrajectory(
            PendingDropRecord record,
            int elapsedTicksAtDropletSpawn,
            List<DropletInstanceDto> droplets)
        {
            double x = record.StartX;
            double y = record.StartY;
            double vx = record.InitialVx;
            double vy = record.InitialVy;

            var existingCollisions = elapsedTicksAtDropletSpawn > 0
                ? record.Collisions.Where(c => c.Tick < elapsedTicksAtDropletSpawn).ToList()
                : [];

            var startTick = 0;
            if (elapsedTicksAtDropletSpawn > 0 && record.Keyframes.Count > 0)
            {
                var priorPoint = record.Keyframes
                    .Where(k => k.Tick <= elapsedTicksAtDropletSpawn)
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
                    startTick = Math.Max(0, elapsedTicksAtDropletSpawn);
                }
            }

            var keyframes = startTick > 0
                ? record.Keyframes.Where(k => k.Tick < startTick).ToList()
                : [];

            var hitDropletIds = new HashSet<string>(StringComparer.Ordinal);
            var bounciness = Math.Clamp(record.Bounciness > 0 ? record.Bounciness : 0.85, 0.1, 5.0);
            var parachuteMass = Math.Clamp(record.Mass > 0 ? record.Mass : 1.0, 0.1, 10.0);
            var maxAllowedTicks = Math.Max(record.TotalTicks, 30) + 180; // Bounded cap (~3 seconds extra if vertical speed was dampened)
            var finalTick = startTick;
            var bouncedOffTargetRim = false;

            for (var tick = startTick; tick <= maxAllowedTicks; tick++)
            {
                finalTick = tick;
                var t = tick * Dt;

                // Check collisions with any active droplets at this tick
                if (droplets.Count > 0 && y < record.LandingY - 0.02)
                {
                    var localTick = tick - elapsedTicksAtDropletSpawn;
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
                            var deltaVy = Math.Clamp((ny * impulseMagnitude * 0.18) + (relVy * 0.06), -0.06, 0.08);

                            vx = Math.Clamp(vx + deltaVx, -MaxVx, MaxVx);
                            vy = Math.Clamp(vy + deltaVy, MinVy, Math.Max(MaxVy, record.InitialVy * 1.15));

                            existingCollisions.Add(new ParachuteCollisionEventDto
                            {
                                Tick = tick,
                                DropletId = droplet.Id,
                                X = Math.Round(x, 4),
                                Y = Math.Round(y, 4),
                                DeltaVx = Math.Round(deltaVx, 4),
                                DeltaVy = Math.Round(deltaVy, 4),
                                SizeScale = droplet.SizeScale
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

                // Step physics: constant directional velocity along drop angle (no sinusoidal sway)
                vx = Math.Clamp(vx * 0.998, -MaxVx, MaxVx);
                vy = Math.Clamp(record.InitialVy + (vy - record.InitialVy) * 0.96, MinVy, Math.Max(MaxVy, record.InitialVy * 1.15)); // Canopy stabilizes vertical speed

                x += vx * Dt;
                y += vy * Dt;

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
            record.DropletHitCount = existingCollisions.Count;
            record.Keyframes = keyframes;

            (record.Score, record.IsBullseye, record.IsWin, record.IsHit) =
                CalculateLandingScore(record.LandingX, record.TargetCenterX, record.TargetHalfWidth);
        }

        /// <summary>
        /// Evaluates landing accuracy against the target platform on a 0 - 100 scale.
        /// 0 = missed the platform
        /// 1 = outer edge of the platform
        /// 100 = dead center of the platform (bullseye)
        /// </summary>
        public static (int Score, bool IsBullseye, bool IsWin, bool IsHit) CalculateLandingScore(
            double landingX,
            double targetCenterX,
            double targetHalfWidth)
        {
            var distanceFromCenter = Math.Round(Math.Abs(landingX - targetCenterX), 4);
            var isHit = distanceFromCenter <= targetHalfWidth;

            if (isHit)
            {
                var ratio = Math.Clamp(distanceFromCenter / Math.Max(0.0001, targetHalfWidth), 0.0, 1.0);
                var score = Math.Clamp((int)Math.Round(100.0 - (ratio * 99.0), MidpointRounding.AwayFromZero), 1, 100);
                var isBullseye = score == 100;
                return (score, isBullseye, true, true);
            }

            return (0, false, false, false);
        }
    }
}

