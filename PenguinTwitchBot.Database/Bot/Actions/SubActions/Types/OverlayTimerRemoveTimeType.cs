using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Overlay Timer - Remove Time",
        description: "Removes time from the on-stream timer overlay",
        icon: "mdi-timer-minus",
        color: "Warning",
        tableName: "subactions_overlay_timer_removetime")]
    public class OverlayTimerRemoveTimeType : SubActionType, ISubActionUIProvider
    {
        public OverlayTimerRemoveTimeType()
        {
            SubActionTypes = SubActionTypes.OverlayTimerRemoveTime;
        }

        public string Amount { get; set; } = "60";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(Amount),
                    Label = "Time to remove",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "Seconds or hh:mm:ss. Supports %variables%. The timer never goes below zero."
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            ];
        }

        public Dictionary<string, object?> GetValues() => new()
        {
            { nameof(Amount), Amount },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Amount), out var amount)) Amount = amount as string ?? "60";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(Amount), out var amount) || string.IsNullOrWhiteSpace(amount as string))
                return "Time to remove is required.";

            return null;
        }
    }
}
