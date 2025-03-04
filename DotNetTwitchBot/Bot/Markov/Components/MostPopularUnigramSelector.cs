namespace DotNetTwitchBot.Bot.Markov.Components
{
    public class MostPopularUnigramSelector : IUnigramSelector
    {
        public string SelectUnigram(IEnumerable<string> ngrams)
        {
            return ngrams
                .GroupBy(a => a).OrderByDescending(a => a.Count())
                .First()
                .First();

        }
    }
}
