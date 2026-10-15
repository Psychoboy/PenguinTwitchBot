using System.Security.Cryptography;

namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    public static class DropGameMath
    {
        public static double RandomDouble(double min, double max)
        {
            var u = RandomNumberGenerator.GetInt32(0, 1_000_000) / 1_000_000.0;
            return min + ((max - min) * u);
        }
    }
}

