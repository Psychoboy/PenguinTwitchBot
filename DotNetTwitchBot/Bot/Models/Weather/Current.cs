using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Weather
{
    public class Current
    {
        [JsonPropertyName("temp_c")]
        public double TempC { get; set; }
        [JsonPropertyName("temp_f")]
        public double TempF { get; set; }
        [JsonPropertyName("condition")]
        public Condition Condition { get; set; } = new Condition();
        [JsonPropertyName("wind_mph")]
        public double WindMph { get; set; }
        [JsonPropertyName("wind_kph")]
        public double WindKph { get; set; }
        [JsonPropertyName("humidity")]
        public int Humidity { get; set; }
        [JsonPropertyName("feelslike_c")]
        public double FeelsLikeC { get; set; }
        [JsonPropertyName("feelslike_f")]
        public double? FeelsLikeF { get; set; }
    }
}