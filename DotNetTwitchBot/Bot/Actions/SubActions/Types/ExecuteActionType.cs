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
        public string ActionId { get; set; } = "";

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
            var actionOptions = actions.Select(a => new SelectOption 
            { 
                Name = a.Name, 
                Id = a.Id?.ToString() ?? string.Empty 
            }).ToList();

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
                { nameof(ActionId), ActionId },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(ActionId), out var actionId))
            {
                ActionId = actionId?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(Enabled), out var enabled))
            {
                Enabled = enabled as bool? ?? true;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(ActionId), out var actionId) || string.IsNullOrWhiteSpace(actionId?.ToString()))
            {
                return "Action to Execute is required";
            }

            return null;
        }
    }
}
