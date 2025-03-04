using DotNetTwitchBot.Extensions;

namespace DotNetTwitchBot.Bot.Markov.Components
{
    public class UnweightedRandomUnigramSelector : IUnigramSelector
    {
        public string SelectUnigram(IEnumerable<string> ngrams)
        {
            return ngrams.GroupBy(a => a).RandomElement().First();
        }
    }
}
