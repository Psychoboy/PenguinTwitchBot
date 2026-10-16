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

        public static List<PlinkoWaypointDto> BuildPlinkoWaypoints(int rows, int bucketCount, int targetBucketIndex, double bounciness = 0.75)
        {
            var cols = Math.Max(6, bucketCount);
            var waypoints = new List<PlinkoWaypointDto>(rows + 2);
            var slotJitter = DropGameMath.RandomDouble(0.25, 0.75);
            var targetX = (targetBucketIndex + slotJitter) / bucketCount;

            var pegCols = new int[rows];

            // Determine bottom peg in row (rows - 1) feeding into targetX
            var lastIsOdd = (rows - 1) % 2 == 1;
            var lastCount = lastIsOdd ? cols - 1 : cols;
            var idealLastC = (int)Math.Round(targetX * cols - (lastIsOdd ? 1.0 : 0.5));
            var candidateLastCols = new List<int>();
            for (var offset = -1; offset <= 1; offset++)
            {
                var cand = idealLastC + offset;
                if (cand >= 0 && cand < lastCount)
                {
                    candidateLastCols.Add(cand);
                }
            }
            if (candidateLastCols.Count == 0)
            {
                candidateLastCols.Add(Math.Clamp(idealLastC, 0, lastCount - 1));
            }
            pegCols[rows - 1] = candidateLastCols[RandomNumberGenerator.GetInt32(0, candidateLastCols.Count)];

            // Work backward from bottom peg row up to row 0 along connected peg lattice
            for (var r = rows - 1; r > 0; r--)
            {
                var curC = pegCols[r];
                var curIsOdd = r % 2 == 1;
                var prevIsOdd = (r - 1) % 2 == 1;
                var prevCount = prevIsOdd ? cols - 1 : cols;

                var parentA = curIsOdd ? curC : curC - 1;
                var parentB = curIsOdd ? curC + 1 : curC;

                var validParents = new List<int>();
                if (parentA >= 0 && parentA < prevCount) validParents.Add(parentA);
                if (parentB >= 0 && parentB < prevCount) validParents.Add(parentB);

                if (validParents.Count > 0)
                {
                    pegCols[r - 1] = validParents[RandomNumberGenerator.GetInt32(0, validParents.Count)];
                }
                else
                {
                    pegCols[r - 1] = Math.Clamp(parentA, 0, prevCount - 1);
                }
            }

            // Entry point above the top peg
            var startPegX = (pegCols[0] + 0.5) / cols;
            var startX = Math.Clamp(startPegX + DropGameMath.RandomDouble(-0.012, 0.012), 0.06, 0.94);

            waypoints.Add(new PlinkoWaypointDto
            {
                Row = -1,
                Col = pegCols[0],
                X = Math.Round(startX, 4),
                Y = 0.04
            });

            for (var r = 0; r < rows; r++)
            {
                var isOdd = r % 2 == 1;
                var countInRow = isOdd ? cols - 1 : cols;
                var c = Math.Clamp(pegCols[r], 0, countInRow - 1);
                var pegNormX = isOdd ? (c + 1.0) / cols : (c + 0.5) / cols;
                var pegNormY = 0.16 + (((double)(r + 1) / rows) * 0.64);

                waypoints.Add(new PlinkoWaypointDto
                {
                    Row = r,
                    Col = c,
                    X = Math.Round(pegNormX, 4),
                    Y = Math.Round(pegNormY, 4)
                });
            }

            waypoints.Add(new PlinkoWaypointDto
            {
                Row = rows,
                Col = targetBucketIndex,
                X = Math.Round(targetX, 4),
                Y = 0.96
            });

            return waypoints;
        }
    }
}

