using DotNetTwitchBot.Bot.Markov.Models;

namespace DotNetTwitchBot.Bot.Markov.TokenisationStrategies
{
    public interface IMarkovStrategy
    {

        IEnumerable<string> SplitTokens(string input);

        string RebuildPhrase(IEnumerable<string> tokens);

        void Learn(IEnumerable<string> phrases, bool ignoreAlreadyLearnt = true);

        void Learn(string phrase);

        void Retrain(int newLevel);

        IEnumerable<string> Walk(int lines = 1, string? seed = default);

        string? GetTerminatorUnigram();

        string GetPrepadUnigram();
    }
}
