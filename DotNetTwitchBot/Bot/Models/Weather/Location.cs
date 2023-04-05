using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Weather
{
    public class Location
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("country")]
        public string Country { get; set; } = "";
        [JsonPropertyName("region")]
        public string Region { get; set; } = "";
        [JsonPropertyName("localtime")]
        public string LocalTime { get; set; } = "";
        [JsonPropertyName("localtime_epoch")]
        public int? LocalTimeEpoch { get; set; }
    }
}