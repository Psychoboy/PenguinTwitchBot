namespace DotNetTwitchBot.Bot.Alerts
{
    public class ClipAlert : BaseAlert
    {
        public int Duration { get; set; }
        public string ClipUrl { get; set; } = "";

        public override string Generate()
        {
            return string.Format("{{\"clip\":\"{0}, {1}\"}}", ClipUrl, Duration);
        }

        public override string Generate(string fullConfig)
        {
            throw new NotImplementedException();
        }
    }
}
