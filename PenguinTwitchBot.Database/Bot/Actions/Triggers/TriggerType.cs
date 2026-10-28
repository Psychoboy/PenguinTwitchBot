using PenguinTwitchBot.Database.Bot.Actions;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models.Actions.Triggers
{
    public class TriggerType
    {
        [Key]
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;
        public TriggerTypes Type { get; set; }
        public string Configuration { get; set; } = string.Empty; // JSON configuration for trigger-specific data
        public bool Enabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // One-to-many relationship: Trigger belongs to one Action
        public Guid? ActionId { get; set; }
        [JsonIgnore]
        public ActionType? Action { get; set; }

        // Nullable reference columns for efficient querying (instead of JSON deserialization)
        // These are populated based on trigger type and stored alongside Configuration JSON
        public Guid? TimerGroupId { get; set; } // For TriggerTypes.Timer
        public Guid? CommandId { get; set; }     // For TriggerTypes.Command
        public Guid? DefaultCommandId { get; set; } // For TriggerTypes.DefaultCommand
        public Guid? KeywordId { get; set; }     // For TriggerTypes.Keyword
    }
}
