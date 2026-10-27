using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Stop Parachute Session",
        description: "Stops the active Parachute Drop session on the overlay",
        icon: "mdi-timer-stop",
        color: "Warning",
        category: SubActionCategories.Minigames,
        minigameGroup: MinigameGroups.Parachute,
        tableName: "subactions_parachute_stop_session")]
    public class ParachuteStopSessionType : SubActionType, ISubActionUIProvider
    {
        public ParachuteStopSessionType()
        {
            SubActionTypes = SubActionTypes.ParachuteStopSession;
        }

        public bool ClearQueue { get; set; } = true;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new()
            {
                PropertyName = nameof(ClearQueue),
                Label = "Clear Queued Sessions",
                FieldType = UIFieldType.Switch,
                SwitchColor = "Primary",
                HelperText = "Also cancel any queued Parachute Drop sessions waiting to start."
            },
            new()
            {
                PropertyName = nameof(Enabled),
                Label = "Enabled",
                FieldType = UIFieldType.Switch,
                SwitchColor = "Success"
            }
        ];

        public Dictionary<string, object?> GetValues() => new()
        {
            { nameof(ClearQueue), ClearQueue },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(ClearQueue), out var clearQueue)) ClearQueue = clearQueue as bool? ?? true;
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values) => null;
    }
}

