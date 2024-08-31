namespace DotNetTwitchBot.Bot.Alerts
{
    public class StopClip : BaseAlert
    {
        public override string Generate()
        {
            return string.Format("{{\"stopclip\":\"\"}}");
        }

        public override string Generate(string fullConfig)
        {
            throw new NotImplementedException();
        }
    }
}
