using System.Text.RegularExpressions;

namespace DotNetTwitchBot.Bot.Markov.TokenisationStrategies
{
    public class StringMarkov(ILogger<StringMarkov> logger, IServiceScopeFactory scopeFactory) : GenericMarkov(logger, scopeFactory)
    {
        private readonly string pattern = @"(?<=[!.,?])|(?=[!.,?])|\s+"; //Splits on specific punctuation and whitespace

        public override IEnumerable<string> SplitTokens(string input)
        {
            if (input == null)
            {
                return [""];
            }

            input = input.Trim();
            return Regex.Split(input, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)).Where(s => !string.IsNullOrWhiteSpace(s));
        }

        public override string RebuildPhrase(IEnumerable<string> tokens)
        {
            var result = new List<string>();
            foreach (var token in tokens)
            {
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                if (token[0].Equals('!') ||
                    token[0].Equals('.') ||
                    token[0].Equals(',') ||
                    token[0].Equals('?') )
                {
                    result.Add(token);
                    result.Add(" ");
                }
                else
                {
                    if (result.Count > 0)
                    {
                        result.Add(" ");
                    }
                    result.Add(token);
                }
            }
            return string.Join("", result);
        }
    }
}
