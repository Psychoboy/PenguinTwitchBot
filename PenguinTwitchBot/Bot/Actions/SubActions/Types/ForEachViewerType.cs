using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "For Each Viewer",
        description: "Run an action for each viewer in the specified group. Sets %user% to each viewer's username.",
        icon: MdiIcons.AccountGroup,
        color: "Secondary",
        tableName: "subactions_foreachviewer")]
    public class ForEachViewerType : SubActionType, ISubActionUIProvider
    {
        public int? ActionId { get; set; }
        public string ActionName { get; set; } = string.Empty;

        /// <summary>
        /// "AllViewers" = everyone currently in chat, "ActiveViewers" = recently active viewers.
        /// </summary>
        public string ViewerScope { get; set; } = "AllViewers";

        public ForEachViewerType()
        {
            SubActionTypes = SubActionTypes.ForEachViewer;
        }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            List<SelectOption>? actionOptions = null;

            if (serviceProvider != null)
            {
                using var scope = serviceProvider.CreateScope();
                var actionService = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actions = Task.Run(async () => await actionService.GetAllActionsAsync()).GetAwaiter().GetResult();
                actionOptions = actions
                    .Where(a => a.Id.HasValue)
                    .Select(a => new SelectOption { Name = a.Name, Id = a.Id!.Value })
                    .OrderBy(a => a.Name)
                    .ToList();
            }

            return
            [
                new SubActionUIField
                {
                    PropertyName = nameof(ActionId),
                    Label = "Action to Run",
                    FieldType = UIFieldType.Select,
                    SelectOptions = actionOptions,
                    Required = true,
                    Clearable = true,
                    HelperText = "The action to run for each viewer. The %user% variable will be set to each viewer's username."
                },
                new SubActionUIField
                {
                    PropertyName = nameof(ViewerScope),
                    Label = "Viewer Group",
                    FieldType = UIFieldType.Select,
                    Options = ["AllViewers", "ActiveViewers", "Subscribers"],
                    Required = true,
                    HelperText = "AllViewers: everyone currently in chat. ActiveViewers: viewers who have interacted recently. Subscribers: viewers who are subscribed to the channel."
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

        public Dictionary<string, object?> GetValues() => new()
        {
            { nameof(ActionId), ActionId?.ToString() ?? string.Empty },
            { nameof(ActionName), ActionName },
            { nameof(ViewerScope), ViewerScope },
            { nameof(Enabled), Enabled }
        };

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

            if (values.TryGetValue(nameof(ActionName), out var actionName))
                ActionName = actionName?.ToString() ?? string.Empty;

            if (values.TryGetValue(nameof(ViewerScope), out var scope))
                ViewerScope = scope?.ToString() ?? "AllViewers";

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(ActionId), out var actionId) ||
                string.IsNullOrWhiteSpace(actionId?.ToString()) ||
                !int.TryParse(actionId?.ToString(), out var parsedId) || parsedId <= 0)
            {
                return "Action to Run is required";
            }

            return null;
        }
    }
}
