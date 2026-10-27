using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models.Points
{
    [IndexAttribute(nameof(UserId))]
    [IndexAttribute(nameof(Username))]
    [IndexAttribute(nameof(UserId), nameof(PointTypeId))]
    [IndexAttribute(nameof(PointTypeId), nameof(Banned), nameof(Points), IsDescending = new[] { false, false, true })]
    public class UserPoints
    {
        [Key]
        public Guid Id { get; set; }
        public Guid PointTypeId { get; set; }
        [JsonIgnore]
        public PointType PointType { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public long Points { get; set; }
        public bool Banned { get; set; }

    }
}
