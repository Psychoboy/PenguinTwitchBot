using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Toggle Command Disabled",
        description: "Toggle whether a command is enabled or disabled",
        icon: "mdi-toggle-switch",
        color: "Warning",
        tableName: "subactions_togglecommanddisabled")]
    public class ToggleCommandDisabledType : SubActionType, ISubActionUIProvider
    {
        public ToggleCommandDisabledType() { SubActionTypes = SubActionTypes.ToggleCommandDisabledState; }
        public string CommandName { get; set; } = string.Empty;
        public bool IsDisabled { get; set; } = false;
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [
                new SubActionUIField
                {
                    Label = "Command",
                    PropertyName = nameof(CommandName),
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions = []
                },
                new SubActionUIField
                {
                    Label = "Disable Command",
                    PropertyName = nameof(IsDisabled),
                    FieldType = UIFieldType.Switch,
                    Required = true,
                    DefaultValue = false,
                    SwitchColor = "Error"
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

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(CommandName), CommandName },
                { nameof(IsDisabled), IsDisabled },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if(values.TryGetValue(nameof(CommandName), out var commandName)) 
            {
                CommandName = commandName?.ToString() ?? string.Empty;
            }
            if(values.TryGetValue(nameof(IsDisabled), out var isDisabled) && bool.TryParse(isDisabled?.ToString(), out var parsedIsDisabled) ) 
            {
                IsDisabled = parsedIsDisabled;
            }
            if(values.TryGetValue(nameof(Enabled), out var enabled) && bool.TryParse(enabled?.ToString(), out var parsedEnabled) ) 
            {
                Enabled = parsedEnabled;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if(!values.TryGetValue(nameof(CommandName), out var commandName) || string.IsNullOrWhiteSpace(commandName?.ToString()))
            {
                return "Command is required";
            }
            return null;
        }
    }
}
