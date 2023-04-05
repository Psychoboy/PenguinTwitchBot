using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Weather
{
    public class Day
    {
        [JsonPropertyName("maxtemp_c")]
        public double MaxTempC { get; set; }
        [JsonPropertyName("maxtemp_f")]
        public double MaxTempF { get; set; }
        [JsonPropertyName("mintemp_c")]
        public double MinTempC { get; set; }
        [JsonPropertyName("mintemp_f")]
        public double MinTempF { get; set; }
        [JsonPropertyName("condition")]
        public Condition Condition { get; set; } = new Condition();
    }
}