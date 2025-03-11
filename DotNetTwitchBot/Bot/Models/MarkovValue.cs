namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(KeyIndex), IsUnique = false)]
    public class MarkovValue
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string KeyIndex { get; set; } = null!;
        public string Value { get; set; } = "";
    }
}
