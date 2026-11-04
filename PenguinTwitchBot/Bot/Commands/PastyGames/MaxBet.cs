namespace PenguinTwitchBot.Bot.Commands.PastyGames
{

    public class MaxBet
    {
        public enum ParseResult
        {
            Success,
            InvalidValue,
            ToMuch,
            ToLow,
            NotEnough
        }
        public long Amount { get; set; }
        public long MaximumAllowedBet { get; set; } = MaxBetSettings.DefaultMaxBet;
        public ParseResult Result { get; set; }

    }
}
