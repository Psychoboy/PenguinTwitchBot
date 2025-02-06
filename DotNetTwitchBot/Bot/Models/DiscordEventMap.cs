using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models
{
    public class DiscordEventMap
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }

        public ulong DiscordEventId { get; set; }
        public string TwitchEventId { get; set; } = "";
    }
}
