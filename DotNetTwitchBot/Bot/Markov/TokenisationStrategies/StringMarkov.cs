namespace DotNetTwitchBot.Bot.Markov.TokenisationStrategies
{
    public class StringMarkov : GenericMarkov
    {
        public StringMarkov(ILogger<StringMarkov> logger, IServiceScopeFactory scopeFactory)
            : base(logger, scopeFactory)
        { }

        public override IEnumerable<string> SplitTokens(string input)
        {
            if (input == null)
            {
                return new List<string> { "" };
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
