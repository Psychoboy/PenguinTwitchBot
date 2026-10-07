namespace PenguinTwitchBot.Bot.Alerts
{
    public class AlertImage : IBaseAlert
    {

        public string FileName { get; set; } = "";
        public int Duration { get; set; } = 3;
        public float Volume { get; set; } = 0.8F;
        public string CSS { get; set; } = "";
        public string Message { get; set; } = "";

        public string Generate()
        {
            var alertImage = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}, {1}, {2:n1}, {3}, {4}",
                FileName, Duration, Volume, CSS, Message);

            var payload = new Dictionary<string, object>
            {
                ["alert_image"] = alertImage,
                ["ignoreIsPlaying"] = false,
                ["alertChannel"] = ""
            };

            return System.Text.Json.JsonSerializer.Serialize(payload);
        }

        public string Generate(string fullConfig)
        {
            var payload = new Dictionary<string, object>
            {
                ["alert_image"] = fullConfig,
                ["ignoreIsPlaying"] = false
            };

            return System.Text.Json.JsonSerializer.Serialize(payload);
        }
    }
}