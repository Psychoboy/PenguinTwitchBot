
using DotNetTwitchBot.Extensions;

namespace DotNetTwitchBot.Bot.Markov.Components
{
    public class MostPopularRandomUnigramSelector : IUnigramSelector
    {
        public string SelectUnigram(IEnumerable<string> ngrams)
        {
            return ngrams
                .GroupBy(a => a).OrderByDescending(a => a.Count())
                .Take(5).RandomElement().First();
        }
    }
}
