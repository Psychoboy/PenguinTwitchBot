using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Points
{
    [Index(nameof(Name))]
    public class PointType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        [JsonIgnore]
        public ICollection<UserPoints> UserPoints { get; set; } = [];
        public string Name { get; set; } = null!;
        public string Description { get; set; } = "";
        public string AddCommand { get; set; } = null!;
        public string RemoveCommand { get; set; } = null!;
        public string GetCommand { get; set; } = null!;
        public string SetCommand { get; set; } = null!;
        public string AddActiveCommand { get; set; } = null!;
    }
}
