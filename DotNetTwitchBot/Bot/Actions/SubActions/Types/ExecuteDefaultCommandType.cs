using DotNetTwitchBot.Bot.Actions.SubActions.UI;
using DotNetTwitchBot.Bot.Commands;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Execute Default Command",
        description: "Execute a default system command.",
        icon: MdiIcons.Play,
        color: "Primary",
        tableName: "subactions_executedefaultcommand")]
    public class ExecuteDefaultCommandType : SubActionType, ISubActionUIProvider
    {
        public int CommandId { get; set; }
        public bool ElevatedCommand { get; set; }
        public string? RankToExecuteAs { get; set; }

        public ExecuteDefaultCommandType() { SubActionTypes = SubActionTypes.ExecuteDefaultCommand; }
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            if (serviceProvider == null)
            {
                return [];
            }

            using var scope = serviceProvider.CreateScope();
            var commandHandler = scope.ServiceProvider.GetRequiredService<ICommandHandler>();
            var defaultCommands = Task.Run(async() => await commandHandler.GetDefaultCommandsFromDb()).GetAwaiter().GetResult();
            var commands = defaultCommands.Select(a => new SelectOption
            {
                Name = a.CustomCommandName,
                Id = a.Id!.Value
            });
            return [
                new()
                {
                    PropertyName = nameof(CommandId),
                    Label = "Command",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions = [.. commands]
                },
                new()
                {
                    PropertyName = nameof(Text),
                    Label = "Command Parameters",
                    FieldType = UIFieldType.Text,
                    Required = false,
                    HelperText = "Optional parameters to pass to the command. Separate multiple parameters with spaces. Can use variables like %user%."
                },
                new()
                {
                    PropertyName = nameof(ElevatedCommand),
                    Label = "Run with Elevated Rank?",
                    FieldType = UIFieldType.Switch,
                    HelperText = "If enabled, the command will run with elevated rank, allowing it to bypass cooldowns and user level requirements. Use with caution."
                },
                new()
                {
                    PropertyName = nameof(RankToExecuteAs),
                    Label = "Rank Level to Run At",
                    FieldType = UIFieldType.Select,
                    Options = [.. Enum.GetNames<Rank>()],
                    HelperText = "If elevated rank is enabled, execute the command at the selected level."
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                },
                new()
                {
                    PropertyName = "info_hint",
                    Label = "If this is not executed from another command, default values will be set.",
                    FieldType = UIFieldType.Info,
                    Severity = "Info",
                    Dense = true
                },
            ];
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(CommandId), CommandId.ToString() },
                {  nameof(Text), Text  },
                {  nameof(ElevatedCommand), ElevatedCommand  },
                { nameof(RankToExecuteAs), RankToExecuteAs },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(CommandId), out var commandId) && int.TryParse(commandId?.ToString(), out var parsedId))
            {
                CommandId = parsedId;
            }

            if (values.TryGetValue(nameof(Text), out var text))
            {
                Text = text as string ?? "";
            }

            if (values.TryGetValue(nameof(ElevatedCommand), out var elevatedCommand))
            {
                ElevatedCommand = elevatedCommand as bool? ?? false;
            }
    
            if (values.TryGetValue(nameof(RankToExecuteAs), out var permission))
            {
                RankToExecuteAs = permission as string ?? "";
            }

            if (values.TryGetValue(nameof(Enabled), out var enabled))
            {
                Enabled = enabled as bool? ?? true;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(CommandId), out var commandId) ||
                !int.TryParse(commandId?.ToString(), out var parsedId) || parsedId <= 0)
            {
                return "Command to Execute is required";
            }

            if(!values.TryGetValue(nameof(ElevatedCommand), out var elevatedCommand) || (elevatedCommand is bool elevatedCommandBool))
            {
                if(!values.TryGetValue(nameof(RankToExecuteAs), out var rankToExecuteAs) || string.IsNullOrEmpty(rankToExecuteAs as string))
                {
                    return "Rank to Execute As is required when Elevated Command is enabled";
                }
            }

            return null;
        }
    }
}
