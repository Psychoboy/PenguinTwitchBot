using DotNetTwitchBot.Bot.Models.Commands;
using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Points
{
    [Index(nameof(Name))]
    public class PointType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public ICollection<UserPoints> UserPoints { get; set; } = [];
        public string Name { get; set; } = null!;
        public string Description { get; set; } = "";
        //public string AddCommand { get; set; } = null!;
        //public string RemoveCommand { get; set; } = null!;
        //public string GetCommand { get; set; } = null!;
        //public string SetCommand { get; set; } = null!;
        //public string AddActiveCommand { get; set; } = null!;
        //public PointCommand? AddCommand { get; set; }
        //public PointCommand? RemoveCommand { get; set; }
        //public PointCommand? GetCommand { get; set; }
        //public PointCommand? SetCommand { get; set; }
        //public PointCommand? AddActiveCommand { get; set; }
        public ICollection<PointCommand> PointCommands { get; set; } = [];
        public int GetId() { return Id ?? 0; }
    }
}
