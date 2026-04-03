using DotNetTwitchBot.Bot.Actions;
using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Actions.Triggers
{
    public class TriggerType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; } = null!;
        public TriggerTypes Type { get; set; }
        public string Configuration { get; set; } = string.Empty; // JSON configuration for trigger-specific data
        public bool Enabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // One-to-many relationship: Trigger belongs to one Action
        public int? ActionId { get; set; }
        [JsonIgnore]
        public ActionType? Action { get; set; }
    }
}
