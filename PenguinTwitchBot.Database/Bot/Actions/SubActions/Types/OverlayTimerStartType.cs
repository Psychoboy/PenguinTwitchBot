using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Overlay Timer - Start",
        description: "Starts the on-stream timer overlay, optionally setting the direction and starting value",
        icon: "mdi-timer-play",
        color: "Success",
        tableName: "subactions_overlay_timer_start")]
    public class OverlayTimerStartType : SubActionType, ISubActionUIProvider
    {
        public OverlayTimerStartType()
        {
            SubActionTypes = SubActionTypes.OverlayTimerStart;
        }

        /// <summary>"up" counts up, "down" counts down.</summary>
        public string Direction { get; set; } = "up";

        /// <summary>Seconds or hh:mm:ss to start from. Empty resumes from the current value.</summary>
        public string StartTime { get; set; } = "";

        public bool ResetOnStart { get; set; }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(Direction),
                    Label = "Direction",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions =
                    [
                        new SelectOption { Value = "up", Name = "Count up" },
                        new SelectOption { Value = "down", Name = "Count down" }
                    ]
                },
                new()
                {
                    PropertyName = nameof(StartTime),
                    Label = "Start time",
                    FieldType = UIFieldType.Text,
                    HelperText = "Seconds or hh:mm:ss. Supports %variables%. Leave empty to resume from the current value."
                },
                new()
                {
                    PropertyName = nameof(ResetOnStart),
                    Label = "Reset to zero on start",
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
            { nameof(Direction), Direction },
            { nameof(StartTime), StartTime },
            { nameof(ResetOnStart), ResetOnStart },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Direction), out var direction)) Direction = direction as string ?? "up";
            if (values.TryGetValue(nameof(StartTime), out var startTime)) StartTime = startTime as string ?? "";
            if (values.TryGetValue(nameof(ResetOnStart), out var reset)) ResetOnStart = reset as bool? ?? false;
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Direction), out var direction))
            {
                var value = direction as string ?? "";
                if (value != "up" && value != "down") return "Direction must be either count up or count down.";
            }

            return null;
        }
    }
}
