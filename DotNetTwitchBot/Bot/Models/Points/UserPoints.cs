using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Points
{
    [Index(nameof(UserId))]
    [Index(nameof(Username))]
    public class UserPoints
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public int PointTypeId { get; set; }
        public PointType PointType { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public long Points { get; set; }
        public bool Banned { get; set; }

    }
}
