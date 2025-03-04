using DotNetTwitchBot.Extensions;

namespace DotNetTwitchBot.Bot.Markov.Components
{
    public class MostPopulatorUnigramStarterSelector : IUnigramSelector
    {
        public string SelectUnigram(IEnumerable<string> ngrams)
        {
            return ngrams
                .GroupBy(a => a).OrderByDescending(a => a.Count())
                .Take(100).RandomElement().First();
        }
    }
}
