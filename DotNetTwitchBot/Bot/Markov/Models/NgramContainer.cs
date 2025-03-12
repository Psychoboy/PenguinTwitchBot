using System.Text.Json;

namespace DotNetTwitchBot.Bot.Markov.Models
{
    public class NgramContainer
    {
        internal string[] Ngrams { get; }

        public NgramContainer(params string[] args)
        {
            Ngrams = args;
        }

        public override bool Equals(object? o)
        {
            if (o is NgramContainer testObj)
            {
                return Ngrams.OrderBy(a => a).ToArray().SequenceEqual(testObj.Ngrams.OrderBy(a => a).ToArray());
            }

            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                var defaultVal = default(string);
                foreach (var member in Ngrams.Where(a => a != null && !a.Equals(defaultVal)))
                {
                    if(member != null)
                        hash = hash * 23 + member.GetHashCode();
                }
                return hash;
            }
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(Ngrams);
        }
    }
}
