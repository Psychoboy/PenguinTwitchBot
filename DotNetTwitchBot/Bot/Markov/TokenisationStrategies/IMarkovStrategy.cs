using DotNetTwitchBot.Bot.Markov.Models;

namespace DotNetTwitchBot.Bot.Markov.TokenisationStrategies
{
    public interface IMarkovStrategy
    {

        IEnumerable<string> SplitTokens(string input);

        string RebuildPhrase(IEnumerable<string> tokens);

        Task Learn(IEnumerable<string> phrases);

        Task<string> Walk(string? seed = default);
    }
}
