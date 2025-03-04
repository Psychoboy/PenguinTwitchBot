using DotNetTwitchBot.Bot.Markov.Models;

namespace DotNetTwitchBot.Bot.Markov.TokenisationStrategies
{
    public interface IMarkovStrategy
    {

        IEnumerable<string> SplitTokens(string input);

        string RebuildPhrase(IEnumerable<string> tokens);

        void Learn(IEnumerable<string> phrases);

        void Learn(string phrase);

        string Walk(string? seed = default);
    }
}
