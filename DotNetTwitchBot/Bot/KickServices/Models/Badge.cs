using Newtonsoft.Json;

namespace DotNetTwitchBot.Bot.KickServices.Models
{
    public class Badge
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        [JsonProperty("active")]
        public bool Active { get; set; }
    }
}
