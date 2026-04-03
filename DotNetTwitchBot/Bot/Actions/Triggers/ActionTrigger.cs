using DotNetTwitchBot.Bot.Actions;

namespace DotNetTwitchBot.Bot.Models.Actions.Triggers
{
    /// <summary>
    /// Junction table for many-to-many relationship between Actions and Triggers
    /// </summary>
    public class ActionTrigger
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ActionId { get; set; }
        public int TriggerId { get; set; }

        public int Priority { get; set; } = 0; // Execution priority when multiple triggers fire
        public bool Enabled { get; set; } = true; // Can be disabled on a per-action basis while trigger remains globally enabled
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ActionType Action { get; set; } = null!;
        public TriggerType Trigger { get; set; } = null!;
    }
}
