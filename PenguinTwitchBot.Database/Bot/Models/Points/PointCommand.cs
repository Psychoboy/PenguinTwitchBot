using PenguinTwitchBot.Database.Bot.Models.Commands;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models.Points
{
    public enum PointCommandType
    {
        Add,
        Remove,
        Get,
        Set,
        AddActive
    }
    public class PointCommand : BaseCommandProperties
    {
        public new Guid PointTypeId { get; set; }
        [JsonIgnore]
        public new PointType PointType { get; set; } = null!;
        public PointCommandType CommandType { get; set; }
    }
}
