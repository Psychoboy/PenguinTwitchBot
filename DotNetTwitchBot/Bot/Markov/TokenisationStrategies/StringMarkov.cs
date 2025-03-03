namespace DotNetTwitchBot.Bot.Markov.TokenisationStrategies
{
    public class StringMarkov : GenericMarkov<string, string>
    {
        public StringMarkov(ILogger<StringMarkov> logger)
            : base(logger)
        { }

        public override IEnumerable<string> SplitTokens(string input)
        {
            if (input == null)
            {
                return new List<string> { GetPrepadUnigram() };
            }

            input = input.Trim();
            return input.Split(' ');
        }

        public override string RebuildPhrase(IEnumerable<string> tokens)
        {
            return string.Join(" ", tokens);
        }

        public override string? GetTerminatorUnigram()
        {
            return null;
        }

        public override string GetPrepadUnigram()
        {
            return "";
        }
    }
}
