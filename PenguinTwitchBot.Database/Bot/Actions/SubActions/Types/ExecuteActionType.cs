using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Execute Action",
        description: "Execute another action",
        icon: "mdi-play",
        color: "Primary",
        tableName: "subactions_executeaction")]
    public class ExecuteActionType : SubActionType, ISubActionUIProvider
    {
        public int? ActionId { get; set; }
        public string ActionName { get; set; } = string.Empty;
        public bool ElevatedCommand { get; set; }
        public string? RankToExecuteAs { get; set; }

        public ExecuteActionType()
        {
            SubActionTypes = SubActionTypes.ExecuteAction;
        }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [
                new SubActionUIField
                {
                    PropertyName = nameof(ActionId),
                    Label = "Action to Execute",
                    FieldType = UIFieldType.Select,
                    SelectOptions = [],
                    Required = true,
                    Clearable = true
                },
                new()
                {
                    PropertyName = nameof(Text),
                    Label = "Action Parameters",
                    FieldType = UIFieldType.Text,
                    Required = false,
                    HelperText = "Optional parameters to pass to the action. Separate multiple parameters with spaces. Can use variables like %user%."
                },
                new()
                {
                    PropertyName = nameof(ElevatedCommand),
                    Label = "Run with Elevated Rank?",
                    FieldType = UIFieldType.Switch,
                    HelperText = "If enabled, the action will run with elevated rank. Use with caution."
                },
                new()
                {
                    PropertyName = nameof(RankToExecuteAs),
                    Label = "Rank Level to Run At",
                    FieldType = UIFieldType.Select,
                    Options = [],
                    HelperText = "If elevated rank is enabled, execute the action at the selected level."
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
                { nameof(ActionId), ActionId?.ToString() ?? string.Empty },
                { nameof(ActionName), ActionName },
                { nameof(Text), Text },
                { nameof(ElevatedCommand), ElevatedCommand },
                { nameof(RankToExecuteAs), RankToExecuteAs },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(ActionId), out var actionId) && 
                !string.IsNullOrWhiteSpace(actionId?.ToString()) &&
                int.TryParse(actionId?.ToString(), out var parsedId))
            {
                ActionId = parsedId;
            }
            else
            {
                ActionId = null;
            }

            if (values.TryGetValue(nameof(Text), out var text))
            {
                Text = text as string ?? "";
            }

            if (values.TryGetValue(nameof(ElevatedCommand), out var elevatedCommand))
            {
                ElevatedCommand =
                    elevatedCommand as bool? ??
                    (bool.TryParse(elevatedCommand?.ToString(), out var parsedElevatedCommand) && parsedElevatedCommand);
            }

            if (values.TryGetValue(nameof(RankToExecuteAs), out var permission))
            {
                RankToExecuteAs = permission as string ?? "";
            }

            if (values.TryGetValue(nameof(Enabled), out var enabled))
            {
                Enabled = enabled as bool? ?? true;
            }

            if (values.TryGetValue(nameof(ActionName), out var actionName))
            {
                ActionName = actionName?.ToString() ?? string.Empty;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(ActionId), out var actionId) ||
                string.IsNullOrWhiteSpace(actionId?.ToString()) ||
                !int.TryParse(actionId?.ToString(), out var parsedId) || parsedId <= 0)
            {
                return "Action to Execute is required";
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
                    return "Rank to Execute As is required when Elevated Rank is enabled";
                }
            }

            return null;
        }
    }
}
