using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Commands
{
    public class CurrentCooldowns
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public bool IsGlobal { get; set; }
        public DateTime NextGlobalCooldownTime { get; set; } = DateTime.MinValue;
        public DateTime NextUserCooldownTime { get; set; } = DateTime.MinValue;
        public string CommandName { get; set; } = null!;
        public string UserName { get; set; } = ""!;
    }
}
