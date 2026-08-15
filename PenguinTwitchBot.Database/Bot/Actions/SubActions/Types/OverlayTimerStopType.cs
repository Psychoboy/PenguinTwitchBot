using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Overlay Timer - Stop",
        description: "Stops the on-stream timer overlay, optionally resetting it back to zero",
        icon: "mdi-timer-off",
        color: "Error",
        tableName: "subactions_overlay_timer_stop")]
    public class OverlayTimerStopType : SubActionType, ISubActionUIProvider
    {
        public OverlayTimerStopType()
        {
            SubActionTypes = SubActionTypes.OverlayTimerStop;
        }

        public bool ResetOnStop { get; set; }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(ResetOnStop),
                    Label = "Reset to zero on stop",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Warning"
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
            { nameof(ResetOnStop), ResetOnStop },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(ResetOnStop), out var reset)) ResetOnStop = reset as bool? ?? false;
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values) => null;
    }
}
