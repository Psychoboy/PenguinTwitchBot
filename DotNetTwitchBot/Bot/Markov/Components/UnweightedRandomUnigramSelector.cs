namespace DotNetTwitchBot.Bot.Markov.Components
{
    public class UnweightedRandomUnigramSelector<T> : IUnigramSelector<T>
    {
        public T SelectUnigram(IEnumerable<T> ngrams)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return ngrams.GroupBy(a => a)
                .Select(a => a.FirstOrDefault())
                .OrderBy(a => Guid.NewGuid())
                .FirstOrDefault();
#pragma warning restore CS8603 // Possible null reference return.
        }
    }
}
