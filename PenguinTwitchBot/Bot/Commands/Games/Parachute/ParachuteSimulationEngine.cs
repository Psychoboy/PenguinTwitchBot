using PenguinTwitchBot.Bot.Commands.Games.Common;

namespace PenguinTwitchBot.Bot.Commands.Games.Parachute
{
    public static class ParachuteSimulationEngine
    {
        public const double TickRateHz = 60.0;
        public const double Dt = 1.0 / TickRateHz;
        public const double ParachuteRadius = 0.036;

        public static ParachuteTrajectoryPointDto GetParachutePositionAtTick(PendingDropRecord record, int tick)
        {
            if (record.Keyframes.Count > 0)
            {
                var nearest = record.Keyframes.OrderBy(k => Math.Abs(k.Tick - tick)).First();
                return nearest;
            }

            var t = tick * Dt;
            var x = Math.Clamp(record.StartX + record.InitialVx * t + 0.5 * record.WindAccelX * t * t, 0.06, 0.94);
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
            var maxAllowedTicks = Math.Max(record.TotalTicks, 60) + 180; // Bounded cap (~3 seconds extra if vertical speed was dampened)
            var finalTick = startTick;

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

                            // Relative velocity and momentum impulse scaled by droplet mass (size) and speed
                            var relVx = dvx - vx;
                            var relVy = dvy - vy;
                            var impulseMagnitude = 0.085 * droplet.Mass * Math.Max(0.6, droplet.SpeedScale);

                            // Push horizontally in the direction of droplet travel + collision normal
                            var pushDirectionX = Math.Abs(dvx) > 0.04 ? Math.Sign(dvx) : (Math.Abs(nx) > 0.05 ? Math.Sign(nx) : 1.0);
                            var deltaVx = (pushDirectionX * impulseMagnitude) + (relVx * 0.22 * Math.Min(2.5, droplet.Mass));
                            var deltaVy = Math.Clamp((ny * impulseMagnitude * 0.25) + (relVy * 0.08), -0.06, 0.08);

                            vx = Math.Clamp(vx + deltaVx, -0.45, 0.45);
                            vy = Math.Clamp(vy + deltaVy, record.InitialVy * 0.55, record.InitialVy * 1.85);

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

                // Step physics (wind + air drag + sway)
                var swayVelX = record.SwayAmplitude * record.SwayFrequency * Math.Cos((record.SwayFrequency * t) + record.SwayPhase) * 0.35;
                vx = (vx + record.WindAccelX * Dt) * 0.992; // Gentle horizontal air drag
                vy = record.InitialVy + (vy - record.InitialVy) * 0.96; // Canopy stabilizes vertical speed back toward terminal velocity

                x += (vx + swayVelX) * Dt;
                y += vy * Dt;

                // Soft bounce off left/right screen margins
                if (x < 0.05)
                {
                    x = 0.05;
                    vx = Math.Abs(vx) * 0.65;
                }
                else if (x > 0.95)
                {
                    x = 0.95;
                    vx = -Math.Abs(vx) * 0.65;
                }
            }

            record.TotalTicks = finalTick;
            record.LandingX = Math.Round(Math.Clamp(x, 0.05, 0.95), 4);
            record.Collisions = existingCollisions;
            record.DropletHitCount = existingCollisions.Count;
            record.Keyframes = keyframes;

            // Evaluate landing accuracy against Target & Bullseye
            var distanceFromCenter = Math.Abs(record.LandingX - record.TargetCenterX);
            record.IsBullseye = distanceFromCenter <= record.BullseyeHalfWidth;
            record.IsHit = distanceFromCenter <= record.TargetHalfWidth;

            if (record.IsBullseye)
            {
                record.Score = 100;
            }
            else if (record.IsHit)
            {
                var normalizedDist = (distanceFromCenter - record.BullseyeHalfWidth) / Math.Max(0.001, record.TargetHalfWidth - record.BullseyeHalfWidth);
                record.Score = Math.Clamp((int)Math.Round(90.0 - (normalizedDist * 65.0)), 25, 95);
            }
            else
            {
                record.Score = 0;
            }

            record.IsWin = record.WinOnBullseyeOnly ? record.IsBullseye : record.IsHit;
        }
    }
}

