using DotNetTwitchBot.Bot.Actions.SubActions.UI;
using DotNetTwitchBot.Bot.Commands;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Toggle Command Disabled",
        description: "Toggle whether a command is enabled or disabled",
        icon: MdiIcons.ToggleSwitch,
        color: "Warning",
        tableName: "subactions_togglecommanddisabled")]
    public class ToggleCommandDisabledType : SubActionType, ISubActionUIProvider
    {
        public ToggleCommandDisabledType() { SubActionTypes = SubActionTypes.ToggleCommandDisabledState; }
        public int? CommandId { get; set; }
        public bool IsDisabled { get; set; } = false;
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            if (serviceProvider == null)
            {
                return [];
            }
            using var scope = serviceProvider.CreateScope();
            var commandService = scope.ServiceProvider.GetRequiredService<IActionCommandService>();
            var commands = Task.Run(async () => await commandService.GetAllAsync()).GetAwaiter().GetResult();
            var commandOptions = commands
                .Where(a => a.Id.HasValue)
                .Select(a => new SelectOption
                {
                    Name = a.CommandName,
                    Id = a.Id!.Value
                }).OrderBy(a => a.Name).ToList();

            return [
                new SubActionUIField
                {
                    Label = "Command",
                    PropertyName = nameof(CommandId),
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions = commandOptions
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
                { nameof(CommandId), CommandId.ToString() },
                { nameof(IsDisabled), IsDisabled },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if(values.TryGetValue(nameof(CommandId), out var commandId) && int.TryParse(commandId?.ToString(), out var parsedId) ) 
            {
                CommandId = parsedId;
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
            if(!values.TryGetValue(nameof(CommandId), out var commandId) || !int.TryParse(commandId?.ToString(), out var parsedId) || parsedId <= 0 )
            {
                return "Command is required";
            }
            return null;
        }
    }
}
