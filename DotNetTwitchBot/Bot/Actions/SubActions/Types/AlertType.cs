using DotNetTwitchBot.Bot.Actions.SubActions;
using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Alert",
        description: "Show an alert with text, image, or video",
        icon: "mdi-bell",
        color: "Error",
        tableName: "subactions_alert")]
    public class AlertType : SubActionType, ISubActionUIProvider
    {
        public AlertType()
        {
            SubActionTypes = SubActionTypes.Alert;
        }

        public int Duration { get; set; } = 3;
        public float Volume { get; set; } = 0.8f;
        public string CSS { get; set; } = "";

        public string Generate()
        {
            return string.Format("{{\"alert_image\":\"{0}, {1}, {2:n1}, {3}, {4}\",\"ignoreIsPlaying\":false}}",
            File, Duration, Volume, CSS, Text);
        }

        public List<SubActionUIField> GetUIFields()
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(Text),
                    Label = "Alert Text",
                    FieldType = UIFieldType.Text,
                    Required = true
                },
                new()
                {
                    PropertyName = nameof(File),
                    Label = "Image/Video File",
                    FieldType = UIFieldType.Text,
                    HelperText = "Path to media file"
                },
                new()
                {
                    PropertyName = nameof(Duration),
                    Label = "Duration (seconds)",
                    FieldType = UIFieldType.Number,
                    Attributes = new Dictionary<string, object> { { "Min", 1 }, { "Max", 60 } }
                },
                new()
                {
                    PropertyName = nameof(Volume),
                    Label = "Volume",
                    FieldType = UIFieldType.Float,
                    Attributes = new Dictionary<string, object> { { "Min", 0f }, { "Max", 1f }, { "Step", 0.1f } }
                },
                new()
                {
                    PropertyName = nameof(CSS),
                    Label = "Custom CSS",
                    FieldType = UIFieldType.TextArea,
                    Attributes = new Dictionary<string, object> { { "Lines", 2 } }
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    Attributes = new Dictionary<string, object> { { "Color", "Success" } }
                }
            };
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(Text), Text },
                { nameof(File), File },
                { nameof(Duration), Duration },
                { nameof(Volume), Volume },
                { nameof(CSS), CSS },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Text), out var text))
                Text = text as string ?? "";
            if (values.TryGetValue(nameof(File), out var file))
                File = file as string ?? "";
            if (values.TryGetValue(nameof(Duration), out var duration))
                Duration = duration as int? ?? 3;
            if (values.TryGetValue(nameof(Volume), out var volume))
                Volume = volume as float? ?? 0.8f;
            if (values.TryGetValue(nameof(CSS), out var css))
                CSS = css as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(Text), out var text) || string.IsNullOrWhiteSpace(text as string))
                return "Alert text is required";

            var duration = values.TryGetValue(nameof(Duration), out var durationObj) ? (durationObj as int? ?? 3) : 3;
            if (duration < 1 || duration > 60)
                return "Duration must be between 1 and 60 seconds";

            var volume = values.TryGetValue(nameof(Volume), out var volumeObj) ? (volumeObj as float? ?? 0.8f) : 0.8f;
            if (volume < 0 || volume > 1)
                return "Volume must be between 0 and 1";

            return null;
        }
    }
}
