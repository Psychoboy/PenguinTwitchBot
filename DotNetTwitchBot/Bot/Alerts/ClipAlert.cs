namespace DotNetTwitchBot.Bot.Alerts
{
    public class ClipAlert : BaseAlert
    {
        public float Duration { get; set; }
        public string ClipFile { get; set; } = "";
        public string GameImageUrl { get; set; } = "";
        public string StreamerName { get; set; } = "";
        public string StreamerAvatarUrl { get; set; } = "";

        public override string Generate()
        {
            return string.Format("{{\"clip\":\"{0},{1},{2},{3},{4}\"}}", ClipFile, Duration, StreamerName, StreamerAvatarUrl, GameImageUrl);
        }

        public override string Generate(string fullConfig)
        {
            throw new NotImplementedException();
        }
    }
}
