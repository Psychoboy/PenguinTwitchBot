using PenguinTwitchBot.Bot.Commands.Games.Common;

namespace PenguinTwitchBot.Bot.Commands.Games.Plinko
{
    public static class PlinkoSimulationEngine
    {
        public static (int Score, bool IsWin, string Label, bool IsBullseye) EvaluateBucketOutcome(
            List<PlinkoBucket>? buckets,
            int bucketIndex)
        {
            if (buckets == null || buckets.Count == 0)
            {
                return (0, false, "MISS", false);
            }

            var selectedIdx = Math.Clamp(bucketIndex, 0, buckets.Count - 1);
            var b = buckets[selectedIdx];
            var score = b.IsWin ? b.Score : 0;
            var maxWinScore = buckets.Where(x => x.IsWin).Select(x => x.Score).DefaultIfEmpty(0).Max();
            var isBullseye = b.IsWin && b.Score >= maxWinScore;

            return (score, b.IsWin, b.Label, isBullseye);
        }

        public static int PickWeightedBucketIndex(List<PlinkoBucket>? buckets)
        {
            if (buckets == null || buckets.Count == 0)
            {
                return 0;
            }

            var totalWeight = buckets.Sum(b => Math.Max(1, b.Weight));
            var roll = Random.Shared.Next(0, totalWeight);
            var cumulative = 0;
            for (int i = 0; i < buckets.Count; i++)
            {
                cumulative += Math.Max(1, buckets[i].Weight);
                if (roll < cumulative)
                {
                    return i;
                }
            }

            return buckets.Count - 1;
        }
    }
}
