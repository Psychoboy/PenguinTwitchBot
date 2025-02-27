namespace DotNetTwitchBot.Bot
{
    public interface ITools
    {
        string CleanInput(string? strIn);
        string ConvertToCompoundDuration(long seconds);
        int Next(int max);
        int Next(int min, int max);
        int RandomRange(int min, int max);
    }
}