namespace PenguinTwitchBot.Bot.Alerts
{
    public interface IBaseAlert
    {
        public string Generate();
        public string Generate(string fullConfig);
    }
}