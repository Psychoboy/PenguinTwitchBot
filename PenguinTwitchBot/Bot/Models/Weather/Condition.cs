using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Bot.Models.Weather
{
    public class Condition
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }
}