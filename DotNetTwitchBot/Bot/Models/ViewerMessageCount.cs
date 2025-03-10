using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(UserId))]
    public class ViewerMessageCount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = "";
        public long MessageCount { get; set; } = 0;
        public bool banned { get; set; } = false;
    }
}