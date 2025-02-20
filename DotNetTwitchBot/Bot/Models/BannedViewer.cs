using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(Username))]
    public class BannedViewer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        [Unicode(true)]
        public string Username { get; set; } = "";
        public string Reason { get; set; } = "";
    }
}
