using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Overlay Timer - Show/Hide",
        description: "Shows, hides or toggles the visibility of an on-stream timer overlay",
        icon: "mdi-timer-cog",
        color: "Info",
        tableName: "subactions_overlay_timer_visibility")]
    public class OverlayTimerVisibilityType : SubActionType, ISubActionUIProvider
    {
        public const string Show = "show";
        public const string Hide = "hide";
        public const string Toggle = "toggle";

        public OverlayTimerVisibilityType()
        {
            SubActionTypes = SubActionTypes.OverlayTimerVisibility;
        }

        /// <summary>The timer to target. Defaults to "Default". Supports %variables%.</summary>
        public string TimerName { get; set; } = "Default";

        /// <summary>"show", "hide" or "toggle".</summary>
        public string Visibility { get; set; } = Hide;

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
                    PropertyName = nameof(Visibility),
                    Label = "Visibility",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions =
                    [
                        new SelectOption { Value = Show, Name = "Show" },
                        new SelectOption { Value = Hide, Name = "Hide" },
                        new SelectOption { Value = Toggle, Name = "Toggle" }
                    ]
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
            { nameof(Visibility), Visibility },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(TimerName), out var timerName)) TimerName = string.IsNullOrWhiteSpace(timerName as string) ? "Default" : (timerName as string)!.Trim();
            if (values.TryGetValue(nameof(Visibility), out var visibility)) Visibility = visibility as string ?? Hide;
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Visibility), out var visibility))
            {
                var value = visibility as string ?? "";
                if (value != Show && value != Hide && value != Toggle) return "Visibility must be show, hide or toggle.";
            }

            return null;
        }
    }
}
