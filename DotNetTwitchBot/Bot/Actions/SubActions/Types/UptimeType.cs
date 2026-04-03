using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Stream Uptime",
        description: "Get how long the stream has been live",
        icon: MdiIcons.Timer,
        color: "Default",
        tableName: "subactions_uptime")]
    public class UptimeType : SubActionType, ISubActionUIProvider
    {
        public UptimeType()
        {
            SubActionTypes = SubActionTypes.Uptime;
        }

        public List<SubActionUIField> GetUIFields()
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = "info_hint",
                    Label = "The result is available as %uptime% variable.",
                    FieldType = UIFieldType.Info,
                    Severity = "Info",
                    Dense = true
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
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            return null;
        }
    }
}
