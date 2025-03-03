namespace DotNetTwitchBot.Bot.Markov.Models
{
    public class NgramStatistic<TNgram>
    {
        public TNgram Value { get; set; } = default!;
        public double Count { get; set; }
        public double Probability { get; set; }
    }
}
