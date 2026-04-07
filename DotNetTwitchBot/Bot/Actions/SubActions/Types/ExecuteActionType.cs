using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Execute Action",
        description: "Execute another action",
        icon: MdiIcons.Play,
        color: "Primary",
        tableName: "subactions_executeaction")]
    public class ExecuteActionType : SubActionType, ISubActionUIProvider
    {
        public int ActionId { get; set; }
        public string ActionName { get; set; } = string.Empty;

        public ExecuteActionType()
        {
            SubActionTypes = SubActionTypes.ExecuteAction;
        }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            if (serviceProvider == null)
            {
                return [];
            }

            using var scope = serviceProvider.CreateScope();
            var actionService = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
            var actions = Task.Run(async () => await actionService.GetAllActionsAsync()).GetAwaiter().GetResult();
            var actionOptions = actions
                .Where(a => a.Id.HasValue)
                .Select(a => new SelectOption 
                { 
                    Name = a.Name, 
                    Id = a.Id!.Value 
                }).OrderBy(a => a.Name).ToList();

            return [
                new SubActionUIField
                {
                    PropertyName = nameof(ActionId),
                    Label = "Action to Execute",
                    FieldType = UIFieldType.Select,
                    SelectOptions = actionOptions,
                    Required = true
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
                { nameof(ActionId), ActionId.ToString() },
                { nameof(Enabled), Enabled },
                { nameof(ActionName), ActionName }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(ActionId), out var actionId) && int.TryParse(actionId?.ToString(), out var parsedId))
            {
                ActionId = parsedId;
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
                !int.TryParse(actionId?.ToString(), out var parsedId) || parsedId <= 0)
            {
                return "Action to Execute is required";
            }

            return null;
        }
    }
}
