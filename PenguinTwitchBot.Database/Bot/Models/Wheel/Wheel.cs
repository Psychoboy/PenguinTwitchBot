using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models.Wheel
{
    public class Wheel
    {
        [Key]
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public List<WheelProperty> Properties { get; set; } = new List<WheelProperty>();
        public string WinningMessage { get; set; } = "The prize is {label}!";
    }
}
