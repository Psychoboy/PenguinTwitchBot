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
    }
}
