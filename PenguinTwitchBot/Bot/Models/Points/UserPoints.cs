using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Bot.Models.Points
{
    [Index(nameof(UserId))]
    [Index(nameof(Username))]
    [Index(nameof(UserId), nameof(PointTypeId))]
    [Index(nameof(PointTypeId), nameof(Banned), nameof(Points), IsDescending = new[] { false, false, true })]
    public class UserPoints
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        [JsonIgnore]
        public int PointTypeId { get; set; }
        [JsonIgnore]
        public PointType PointType { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public long Points { get; set; }
        public bool Banned { get; set; }

    }
}
