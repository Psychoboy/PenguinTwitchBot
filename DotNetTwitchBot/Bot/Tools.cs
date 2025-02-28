using Microsoft.Identity.Client;

namespace DotNetTwitchBot.Bot
{
    public class Tools : ITools
    {
        public int Next(int max)
        {
            return StaticTools.Next(max);
        }

        public int Next(int min, int max)
        {
            return StaticTools.Next(min, max);
        }

        public int RandomRange(int min, int max)
        {
            return StaticTools.RandomRange(min, max);
        }

        public string ConvertToCompoundDuration(long seconds)
        {
            return StaticTools.ConvertToCompoundDuration(seconds);
        }

        public string CleanInput(string? strIn)
        {
            return StaticTools.CleanInput(strIn);
        }

        public void Sleep(int milliseconds)
        {
            Thread.Sleep(milliseconds);
        }
    }
}
