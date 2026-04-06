using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Delay",
        description: "Delay the action execution for a specified duration",
        icon: MdiIcons.Timer,
        color: "Default",
        tableName: "subactions_delay")]
    public class DelayType : SubActionType, ISubActionUIProvider
    {
        public DelayType()
        {
            SubActionTypes = SubActionTypes.Delay;
        }
        public string Duration { get; set; } = "10";
        public string Generate()
        {
            return Duration.ToString();
        }
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(Duration),
                    Label = "Duration (seconds)",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "You can enter a %variable% or a number. The value is in milliseconds."
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            };
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(Duration), Duration },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if(values.TryGetValue(nameof(Duration), out var duration))
                Duration = duration as string ?? "10";
            if(values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Duration), out var duration))
            {
                var durationStr = duration as string ?? "";
                if (string.IsNullOrWhiteSpace(durationStr))
                    return "Duration is required.";
                if(durationStr.Contains(' '))
                {
                    return "Duration cannot contain spaces.";
                }
            }
            return null;
        }
    }
}
