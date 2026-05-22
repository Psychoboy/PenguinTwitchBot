using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Application.WheelSpinNotifications
{
    public class WheelSpinComplete
    {
        [JsonPropertyName("wheel")]
        public string Wheel { get; set; } = "";
        [JsonPropertyName("item")]
        public int Index { get; set; } = 0;
        //{"wheel":"stop","item":2}
    }
}
