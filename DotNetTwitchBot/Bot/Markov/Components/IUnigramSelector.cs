namespace DotNetTwitchBot.Bot.Markov.Components
{
    public interface IUnigramSelector<TUnigram>
    {
        TUnigram SelectUnigram(IEnumerable<TUnigram> ngrams);
    }
}
