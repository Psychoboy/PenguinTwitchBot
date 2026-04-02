using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Actions
{
    public class ActionType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string Name { get; set; } = null!;
        public string Group { get; set; } = string.Empty;
        public bool Enbled { get; set; } = true;
        public bool RandomAction { get; set; } = false;
        public bool ConcurrentAction { get; set; } = false;
        public List<SubActionType> SubActions { get; set; } = [];
    }
}
