using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Weather
{
    public class Forecast
    {
        [JsonPropertyName("forecastday")]
        public List<ForecastDay> ForecastDay { get; set; } = new List<ForecastDay>();
    }
}