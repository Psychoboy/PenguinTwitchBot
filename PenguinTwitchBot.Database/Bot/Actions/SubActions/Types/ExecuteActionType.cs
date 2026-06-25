using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
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

        public ExecuteActionType()
        {
            SubActionTypes = SubActionTypes.ExecuteAction;
        }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [];
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(ActionId), ActionId?.ToString() ?? string.Empty },
                { nameof(Enabled), Enabled },
                { nameof(ActionName), ActionName }
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

            return null;
        }
    }
}
