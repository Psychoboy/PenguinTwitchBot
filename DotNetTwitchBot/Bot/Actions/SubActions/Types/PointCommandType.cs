using DotNetTwitchBot.Bot.Actions.SubActions.UI;
using DotNetTwitchBot.Bot.Core.Points;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Point Command",
        description: "Run a point command.",
        icon: MdiIcons.Coin,
        color: "Warning",
        tableName: "subactions_pointcommand")]
    public class PointCommandType : SubActionType, ISubActionUIProvider
    {
        public PointCommandType() { SubActionTypes = SubActionTypes.ExecutePointCommand; }
        public string Arguments { get; set; } = string.Empty;
        public bool RespondToChat { get; set; } = true;
        public bool ElevatedCommand { get; set; }
        public string? RankToExecuteAs { get; set; }
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            if (serviceProvider == null)
            {
                return [];
            }

            using var scope = serviceProvider.CreateScope();
            var commandService = scope.ServiceProvider.GetRequiredService<IPointsSystem>();
            var commands = Task.Run(async () => await commandService.GetAllPointCommands()).GetAwaiter().GetResult();
            var pointNames = commands.Select(x => x.CommandName).ToArray();

            return [
                new()
                {
                    PropertyName = nameof(Text),
                    Label = "Point Command Name",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    Options = pointNames
                },
                new()
                {
                    PropertyName = nameof(Arguments),
                    Label = "Command Arguments",
                    FieldType = UIFieldType.Text,
                    HelperText = "Optional arguments to pass to the command. Separate multiple arguments with spaces. Typical usage is %user% AMOUNT."
                },
                new()
                {
                    PropertyName = nameof(RespondToChat),
                    Label = "Respond to Chat?",
                    FieldType = UIFieldType.Switch,
                    HelperText = "If enabled, the bot will respond in chat with the result of the command execution."
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
            ];
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(Text), Text },
                { nameof(Arguments), Arguments },
                { nameof(RespondToChat), RespondToChat },
                { nameof(ElevatedCommand), ElevatedCommand },
                { nameof(RankToExecuteAs), RankToExecuteAs },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if(values.TryGetValue(nameof(Text), out var textValue) && textValue is string text)
            {
                Text = text;
            }
            if(values.TryGetValue(nameof(Arguments), out var argumentsValue) && argumentsValue is string arguments)
            {
                Arguments = arguments;
            }
            if (values.TryGetValue(nameof(ElevatedCommand), out var elevatedCommandValue) && elevatedCommandValue is bool elevatedCommand)
            {
                ElevatedCommand = elevatedCommand;
            }
            if(values.TryGetValue(nameof(RankToExecuteAs), out var rankToExecuteAsValue) && rankToExecuteAsValue is string rankToExecuteAs)
            {
                RankToExecuteAs = rankToExecuteAs;
            }

            if(values.TryGetValue(nameof(RespondToChat), out var respondToChatValue) && respondToChatValue is bool respondToChat)
            {
                RespondToChat = respondToChat;
            }

            if(values.TryGetValue(nameof(Enabled), out var enabledValue) && enabledValue is bool enabled)
            {
                Enabled = enabled;
            }
        }
        public string? Validate(Dictionary<string, object?> values)
        {
            if(!values.TryGetValue(nameof(Text), out var textValue) || textValue is not string text || string.IsNullOrWhiteSpace(text))
            {
                return "Point Command Name is required.";
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
