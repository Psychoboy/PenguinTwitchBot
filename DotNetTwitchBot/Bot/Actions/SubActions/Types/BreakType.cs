using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Break",
        description: "Stops processing the current subactions, this only works for Sequential Actions",
        icon: MdiIcons.Pause,
        color: "Warning",
        tableName: "subactions_break")]
    public class BreakType : SubActionType, ISubActionUIProvider
    {
        public BreakType() { SubActionTypes = SubActionTypes.Break; }
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = "info_hint",
                    Label = "No variables set, just breaks out from running more subactions in the action.",
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
