namespace DotNetTwitchBot.Bot.Markov.TokenisationStrategies
{
    public class StringMarkov(ILogger<StringMarkov> logger, IServiceScopeFactory scopeFactory) : GenericMarkov(logger, scopeFactory)
    {
        public override IEnumerable<string> SplitTokens(string input)
        {
            if (input == null)
            {
                return [""];
            }

            input = input.Trim();
            return input.Split(' ');
        }

        public override string RebuildPhrase(IEnumerable<string> tokens)
        {
            return string.Join(" ", tokens);
        }
    }
}
