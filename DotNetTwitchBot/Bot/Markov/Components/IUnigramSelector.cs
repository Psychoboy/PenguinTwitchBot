namespace DotNetTwitchBot.Bot.Markov.Components
{
    public interface IUnigramSelector
    {
        string SelectUnigram(IEnumerable<string> ngrams);
    }
}
