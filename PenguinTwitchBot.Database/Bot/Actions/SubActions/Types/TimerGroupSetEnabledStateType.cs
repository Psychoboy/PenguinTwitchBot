using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Toggle Timer Group Enabled",
        description: "Enable or disable a timer group",
        icon: "mdi-timer",
        color: "Info",
        tableName: "subactions_timergroupsetenabled")]
    public class TimerGroupSetEnabledStateType : SubActionType, ISubActionUIProvider
    {
        public TimerGroupSetEnabledStateType() { SubActionTypes = SubActionTypes.TimerGroupSetEnabledState; }
        public int? TimerGroupId { get; set; }
        public string TimerGroupName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [
                new SubActionUIField
                {
                    Label = "Timer Group",
                    PropertyName = nameof(TimerGroupId),
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions = []
                },
                new SubActionUIField
                {
                    Label = "Enable Timer Group",
                    PropertyName = nameof(IsEnabled),
                    FieldType = UIFieldType.Switch,
                    Required = true,
                    DefaultValue = true,
                    SwitchColor = "Success"
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
                { nameof(TimerGroupId), TimerGroupId.ToString() },
                { nameof(TimerGroupName), TimerGroupName },
                { nameof(IsEnabled), IsEnabled },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(TimerGroupId), out var timerGroupId) && int.TryParse(timerGroupId?.ToString(), out var parsedId))
            {
                TimerGroupId = parsedId;
            }
            if (values.TryGetValue(nameof(TimerGroupName), out var timerGroupName))
            {
                TimerGroupName = timerGroupName?.ToString() ?? string.Empty;
            }
            if (values.TryGetValue(nameof(IsEnabled), out var isEnabled) && bool.TryParse(isEnabled?.ToString(), out var parsedIsEnabled))
            {
                IsEnabled = parsedIsEnabled;
            }
            if (values.TryGetValue(nameof(Enabled), out var enabled) && bool.TryParse(enabled?.ToString(), out var parsedEnabled))
            {
                Enabled = parsedEnabled;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(TimerGroupId), out var timerGroupId) || !int.TryParse(timerGroupId?.ToString(), out var parsedId) || parsedId <= 0)
            {
                return "Timer Group is required";
            }
            return null;
        }
    }
}
