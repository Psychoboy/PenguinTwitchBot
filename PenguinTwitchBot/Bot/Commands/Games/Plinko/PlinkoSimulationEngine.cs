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
            var targetX = (targetBucketIndex + 0.5) / bucketCount;
            var currentX = Math.Clamp(0.5 + DropGameMath.RandomDouble(-0.08, 0.08), 0.25, 0.75);

            waypoints.Add(new PlinkoWaypointDto
            {
                Row = -1,
                Col = 0,
                X = Math.Round(currentX, 4),
                Y = 0.08
            });

            for (var r = 0; r < rows; r++)
            {
                var progress = (double)(r + 1) / rows;
                var rowY = 0.16 + (progress * 0.64);
                var remainingRows = rows - r;

                // Guide token smoothly toward targetX while bouncing left/right off pegs
                var idealStep = (targetX - currentX) / Math.Max(1, remainingRows);
                var jitter = (RandomNumberGenerator.GetInt32(0, 2) == 0 ? -1 : 1) * (0.032 * (1.0 - (progress * 0.5)));
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

