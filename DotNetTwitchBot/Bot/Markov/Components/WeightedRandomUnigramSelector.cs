namespace DotNetTwitchBot.Bot.Markov.Components
{
    public class WeightedRandomUnigramSelector<T> : IUnigramSelector<T>
    {
        public T SelectUnigram(IEnumerable<T> ngrams)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return ngrams.OrderBy(a => Guid.NewGuid()).FirstOrDefault();
#pragma warning restore CS8603 // Possible null reference return.
        }
    }
}
