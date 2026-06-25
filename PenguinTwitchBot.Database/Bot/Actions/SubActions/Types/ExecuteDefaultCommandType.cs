using PenguinTwitchBot.Bot.Actions.SubActions.UI;


namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Execute Default Command",
        description: "Execute a default system command.",
        icon: "mdi-play",
        color: "Primary",
        tableName: "subactions_executedefaultcommand")]
    public class ExecuteDefaultCommandType : SubActionType, ISubActionUIProvider
    {
        public string CommandName { get; set; } = null!;
        public bool ElevatedCommand { get; set; }
        public string? RankToExecuteAs { get; set; }

        public ExecuteDefaultCommandType() { SubActionTypes = SubActionTypes.ExecuteDefaultCommand; }
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [];
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(CommandName), CommandName },
                {  nameof(Text), Text  },
                {  nameof(ElevatedCommand), ElevatedCommand  },
                { nameof(RankToExecuteAs), RankToExecuteAs },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(CommandName), out var commandName))
            {
                CommandName = commandName as string ?? "";
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
            if (!values.TryGetValue(nameof(CommandName), out var commandName) ||
                string.IsNullOrWhiteSpace(commandName?.ToString()))
            {
                return "Command to Execute is required";
            }

            var elevatedCommand = false;
            if (values.TryGetValue(nameof(ElevatedCommand), out var elevatedValue))
            {
                elevatedCommand =
                    elevatedValue as bool? ??
                    (bool.TryParse(elevatedValue?.ToString(), out var parsedElevatedCommand) && parsedElevatedCommand);
            }

            if (elevatedCommand)
            {
                if (!values.TryGetValue(nameof(RankToExecuteAs), out var rankToExecuteAs) ||
                    string.IsNullOrWhiteSpace(rankToExecuteAs?.ToString()))
                {
                    return "Rank to Execute As is required when Elevated Command is enabled";
                }
            }

            return null;
        }
    }
}
