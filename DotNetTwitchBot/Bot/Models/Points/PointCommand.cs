using DotNetTwitchBot.Bot.Models.Commands;
using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Points
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
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public new int? Id { get; set; }
        public new int PointTypeId { get; set; }
        public new PointType PointType { get; set; } = null!;
        public PointCommandType CommandType { get; set; }
    }
}
