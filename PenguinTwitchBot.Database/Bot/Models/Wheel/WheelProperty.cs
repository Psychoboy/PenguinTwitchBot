using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models.Wheel
{
    public class WheelProperty
    {
        [Key]
        public Guid Id { get; set; }
        public string Label { get; set; } = "";
        public string? BackgroundColor { get; set; }
        public int Value { get; set; }
        public float Weight { get; set; } = 1;
        public float Order { get; set; } = 1;

        public Guid WheelId { get; set; }
        [JsonIgnore]
        public Wheel Wheel { get; set; } = null!;
    }
}
