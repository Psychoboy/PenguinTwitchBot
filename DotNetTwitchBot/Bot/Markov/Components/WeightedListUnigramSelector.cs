
using DotNetTwitchBot.Bot.Markov.Models;

namespace DotNetTwitchBot.Bot.Markov.Components
{
    public class WeightedListUnigramSelector : IUnigramSelector
    {
        public string SelectUnigram(IEnumerable<string> ngrams)
        {
            var weightedNgrams = ngrams.GroupBy(a => a).Select(a => new WeightedListItem<string>(a.Key, a.Count())).ToList();

            var weightedList = new WeightedList<string>(weightedNgrams);
            var result = weightedList.Next();
            return result ?? "";
        }
    }
}
