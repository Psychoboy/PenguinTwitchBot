using System.Security.Cryptography;
using PenguinTwitchBot.Bot.Commands.Games.Common;

namespace PenguinTwitchBot.Bot.Commands.Games.Plinko
{
    public static class PlinkoSimulationEngine
    {
        public static int PickWeightedBucketIndex(List<PlinkoBucket> buckets)
        {
            if (buckets == null || buckets.Count == 0) return 0;
            var totalWeight = buckets.Sum(b => Math.Max(1, b.Weight));
            var roll = RandomNumberGenerator.GetInt32(0, totalWeight);
            var cumulative = 0;
            for (var i = 0; i < buckets.Count; i++)
            {
                cumulative += Math.Max(1, buckets[i].Weight);
                if (roll < cumulative)
                {
                    return i;
                }
            }
            return buckets.Count / 2;
        }

        public static List<PlinkoWaypointDto> BuildPlinkoWaypoints(int rows, int bucketCount, int targetBucketIndex)
        {
            var waypoints = new List<PlinkoWaypointDto>(rows + 2);
            var slotJitter = DropGameMath.RandomDouble(0.25, 0.75);
            var targetX = (targetBucketIndex + slotJitter) / bucketCount;
            // Widen starting drop point across the top board width (matching PixelPlush's randomized entry)
            var currentX = Math.Clamp(DropGameMath.RandomDouble(0.12, 0.88), 0.10, 0.90);

            waypoints.Add(new PlinkoWaypointDto
            {
                Row = -1,
                Col = (int)Math.Round(currentX * bucketCount),
                X = Math.Round(currentX, 4),
                Y = 0.04
            });

            for (var r = 0; r < rows; r++)
            {
                var progress = (double)(r + 1) / rows;
                var rowY = 0.14 + (progress * 0.68);
                var remainingRows = rows - r;

                // Guide token smoothly toward targetX while bouncing left/right off pegs with organic physics jitter
                var idealStep = (targetX - currentX) / Math.Max(1, remainingRows);
                var jitter = (RandomNumberGenerator.GetInt32(0, 2) == 0 ? -1 : 1) * DropGameMath.RandomDouble(0.024, 0.052) * (1.0 - (progress * 0.35));
                currentX = Math.Clamp(currentX + idealStep + jitter, 0.08, 0.92);

                waypoints.Add(new PlinkoWaypointDto
                {
                    Row = r,
                    Col = (int)Math.Round(currentX * bucketCount),
                    X = Math.Round(currentX, 4),
                    Y = Math.Round(rowY, 4)
                });
            }

            waypoints.Add(new PlinkoWaypointDto
            {
                Row = rows,
                Col = targetBucketIndex,
                X = Math.Round(targetX, 4),
                Y = 0.88
            });

            return waypoints;
        }
    }
}

