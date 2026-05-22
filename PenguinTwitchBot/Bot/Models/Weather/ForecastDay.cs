using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Bot.Models.Weather
{
    public class ForecastDay
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = "";
        [JsonPropertyName("day")]
        public Day Day { get; set; } = new Day();

    }
}