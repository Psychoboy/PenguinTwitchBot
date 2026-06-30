namespace PenguinTwitchBot.Bot.Alerts
{
    public class ClipAlert : IBaseAlert
    {
        public float Duration { get; set; }
        public string ClipFile { get; set; } = "";
        public string GameImageUrl { get; set; } = "";
        public string StreamerName { get; set; } = "";
        public string StreamerAvatarUrl { get; set; } = "";

        public string Generate()
        {
            return string.Format("{{\"clip\":\"{0},{1},{2},{3},{4}\"}}", ClipFile, Duration, StreamerName, StreamerAvatarUrl, GameImageUrl);
        }

        public string Generate(string fullConfig)
        {
            throw new NotImplementedException();
        }
    }
}
