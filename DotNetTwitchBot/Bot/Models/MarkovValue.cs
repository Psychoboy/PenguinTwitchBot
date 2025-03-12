using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(KeyIndex), IsUnique = false)]
    public class MarkovValue
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public long? Id { get; set; }
        public string KeyIndex { get; set; } = null!;
        public string Value { get; set; } = "";
    }
}
