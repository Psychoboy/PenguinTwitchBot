using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Wheel
{
    public class WheelProperty
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string Label { get; set; } = "";
        public string? BackgroundColor { get; set; }
        public int Value { get; set; }
        public float Weight { get; set; } = 1;
        public float Order { get; set; } = 1;

        [JsonIgnore]
        public int WheelId { get; set; }
        [JsonIgnore]
        public Wheel Wheel { get; set; } = null!;
    }
}
