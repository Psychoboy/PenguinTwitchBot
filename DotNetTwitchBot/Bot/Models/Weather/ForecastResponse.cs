using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Weather
{
    public class ForecastResponse
    {
        [JsonPropertyName("location")]
        public Location Location { get; set; } = new Location();
        [JsonPropertyName("current")]
        public Current Current { get; set; } = new Current();
        [JsonPropertyName("forecast")]
        public Forecast Forecast { get; set; } = new Forecast();
    }
}