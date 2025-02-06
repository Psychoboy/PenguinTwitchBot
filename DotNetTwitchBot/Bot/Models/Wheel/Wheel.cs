using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Wheel
{
    public class Wheel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string Name { get; set; } = null!;
        public List<WheelProperty> Properties { get; set; } = new List<WheelProperty>();
        public string WinningMessage { get; set; } = "The prize is {label}!";
    }
}
