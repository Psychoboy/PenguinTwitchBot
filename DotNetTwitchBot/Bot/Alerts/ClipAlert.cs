namespace DotNetTwitchBot.Bot.Alerts
{
    public class ClipAlert : BaseAlert
    {
        public float Duration { get; set; }
        public string ClipFile { get; set; } = "";

        public override string Generate()
        {
            return string.Format("{{\"clip\":\"{0}, {1}\"}}", ClipFile, Duration);
        }

        public override string Generate(string fullConfig)
        {
            throw new NotImplementedException();
        }
    }
}
