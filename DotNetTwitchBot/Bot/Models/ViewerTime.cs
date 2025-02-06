using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(UserId))]
    public class ViewerTime
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = "";
        public long Time { get; set; } = 0;
        public bool banned { get; set; } = false;
    }
}