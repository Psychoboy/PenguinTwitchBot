namespace DotNetTwitchBot.Bot.Models.Wheel
{
    public class Wheel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string Name { get; set; } = null!;
        public List<WheelProperty> Properties { get; set; } = [];
        public string WinningMessage { get; set; } = "The prize is {label}!";
    }
}
