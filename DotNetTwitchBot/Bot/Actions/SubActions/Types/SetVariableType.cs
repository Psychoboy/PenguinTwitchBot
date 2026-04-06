using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Set Variable",
        description: "Set a variable to a specific value. Use this to store information for later use in the action sequence.",
        icon: MdiIcons.Variable,
        color: "Secondary",
        tableName: "subactions_setvariable")]
    public class SetVariableType : SubActionType, ISubActionUIProvider
    {
        public SetVariableType() { SubActionTypes = SubActionTypes.SetVariable; }
        public string Value { get; set; } = string.Empty;
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [
                new SubActionUIField
                {
                    PropertyName = nameof(Text),
                    Label = "Variable Name",
                    HelperText = "The name of the variable to set. This is how you will reference the variable in other actions.",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    Clearable = true
                },
                new SubActionUIField
                {
                    PropertyName = nameof(Value),
                    Label = "Value",
                    HelperText = "The value to set the variable to. This can be a string, number, or boolean.",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    Clearable = true
                },
                new SubActionUIField
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
                ];
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(Text), Text },
                { nameof(Value), Value  },
                { nameof(Enabled), Enabled },
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if(values.TryGetValue(nameof(Text), out var text))
                Text = text as string ?? string.Empty;
            if(values.TryGetValue(nameof(Value), out var value))
                Value = value as string ?? string.Empty;
            if(values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if(!values.TryGetValue(nameof(Text), out var text) || string.IsNullOrWhiteSpace(text as string))
                return "Variable name is required.";
            if(!values.TryGetValue(nameof(Value), out var value) || string.IsNullOrWhiteSpace(value as string))
                return "Value is required.";
            return null;
        }
    }
}
