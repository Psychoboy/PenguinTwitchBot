using DotNetTwitchBot.Bot.Actions.SubActions;

namespace DotNetTwitchBot.Bot.Models.Actions.SubActions
{
    public class AlertType : SubActionType
    {
        public int Duration { get; set; } = 3;
        public float Volume { get; set; } = 0.8f;
        public string CSS { get; set; } = "";

        public string Generate()
        {
            return string.Format("{{\"alert_image\":\"{0}, {1}, {2:n1}, {3}, {4}\",\"ignoreIsPlaying\":false}}",
            File, Duration, Volume, CSS, Text);
        }
    }
}
