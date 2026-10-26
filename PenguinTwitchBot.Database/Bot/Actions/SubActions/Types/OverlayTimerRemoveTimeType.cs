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

        /// <summary>The timer to target. Defaults to "Default". Supports %variables%.</summary>
        public string TimerName { get; set; } = "Default";

        public string Amount { get; set; } = "60";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(TimerName),
                    Label = "Timer",
                    FieldType = UIFieldType.Text,
                    Required = false,
                    DefaultValue = "Default",
                    HelperText = "Name of the timer to target. Supports %variables%. Leave empty for 'Default'."
                },
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
            { nameof(TimerName), TimerName },
            { nameof(Amount), Amount },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(TimerName), out var timerName)) TimerName = string.IsNullOrWhiteSpace(timerName as string) ? "Default" : (timerName as string)!.Trim();
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
