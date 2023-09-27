namespace DotNetTwitchBot.Bot.Commands.PastyGames
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
        public ParseResult Result { get; set; }

    }
}
