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
            var cols = Math.Max(2, bucketCount);
            targetBucketIndex = Math.Clamp(targetBucketIndex, 0, cols - 1);
            var waypoints = new List<PlinkoWaypointDto>(rows + 2);

            var pegCols = new int[rows];
            var lastIsOdd = (rows - 1) % 2 == 1;
            var lastCount = lastIsOdd ? cols - 1 : cols;

            // Determine the bottom peg(s) that physically feed directly into targetBucketIndex:
            // Odd row pegs are at (c + 1.0)/cols (divider between bucket c and c+1).
            // A peg at c can bounce LEFT into bucket c, or RIGHT into bucket c+1.
            // Even row pegs are at (c + 0.5)/cols (centered above bucket c).
            var validLastPegs = new List<(int Col, double TargetX)>();

            if (lastIsOdd)
            {
                if (targetBucketIndex < cols - 1)
                {
                    // Bounce LEFT from divider peg targetBucketIndex into targetBucketIndex
                    var jitter = DropGameMath.RandomDouble(0.38, 0.65);
                    validLastPegs.Add((targetBucketIndex, (targetBucketIndex + jitter) / cols));
                }
                if (targetBucketIndex - 1 >= 0)
                {
                    // Bounce RIGHT from divider peg targetBucketIndex - 1 into targetBucketIndex
                    var jitter = DropGameMath.RandomDouble(0.35, 0.62);
                    validLastPegs.Add((targetBucketIndex - 1, (targetBucketIndex + jitter) / cols));
                }
            }
            else
            {
                var jitterCenter = DropGameMath.RandomDouble(0.38, 0.62);
                validLastPegs.Add((targetBucketIndex, (targetBucketIndex + jitterCenter) / cols));
            }

            if (validLastPegs.Count == 0)
            {
                validLastPegs.Add((Math.Clamp(targetBucketIndex, 0, lastCount - 1), (targetBucketIndex + 0.5) / cols));
            }

            var chosen = validLastPegs[RandomNumberGenerator.GetInt32(0, validLastPegs.Count)];
            pegCols[rows - 1] = chosen.Col;
            var targetX = chosen.TargetX;

            // Work backward from bottom peg row up to row 0 along connected peg lattice
            for (var r = rows - 1; r > 0; r--)
            {
                var curC = pegCols[r];
                var curIsOdd = r % 2 == 1;
                var prevIsOdd = (r - 1) % 2 == 1;
                var prevCount = prevIsOdd ? cols - 1 : cols;

                var parentLeft = curIsOdd ? curC : curC - 1;
                var parentRight = curIsOdd ? curC + 1 : curC;

                var validParents = new List<int>();
                if (parentLeft >= 0 && parentLeft < prevCount) validParents.Add(parentLeft);
                if (parentRight >= 0 && parentRight < prevCount) validParents.Add(parentRight);

                if (validParents.Count > 0)
                {
                    pegCols[r - 1] = validParents[RandomNumberGenerator.GetInt32(0, validParents.Count)];
                }
                else
                {
                    pegCols[r - 1] = Math.Clamp(parentLeft, 0, prevCount - 1);
                }
            }

            // Entry point above the top peg
            var startPegX = (pegCols[0] + 0.5) / cols;
            var startX = Math.Clamp(startPegX + DropGameMath.RandomDouble(-0.012, 0.012), 0.04, 0.96);

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

