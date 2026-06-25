namespace PenguinTwitchBot.Bot.Alerts
{
    public abstract class BaseAlert
    {
        public abstract string Generate();
        public abstract string Generate(string fullConfig);
    }
}