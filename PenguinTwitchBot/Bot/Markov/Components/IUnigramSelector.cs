namespace PenguinTwitchBot.Bot.Markov.Components
{
    public interface IUnigramSelector
    {
        string SelectUnigram(IEnumerable<string> ngrams);
    }
}
