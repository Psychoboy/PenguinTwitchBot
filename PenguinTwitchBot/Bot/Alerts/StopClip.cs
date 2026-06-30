namespace PenguinTwitchBot.Bot.Alerts
{
    public class StopClip : IBaseAlert
    {
        public string Generate()
        {
            return "{\"stopclip\":\"\"}";
        }

        public string Generate(string fullConfig)
        {
            throw new NotImplementedException();
        }
    }
}

