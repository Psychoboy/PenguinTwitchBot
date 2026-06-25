using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Raffle Get Entry Count",
        description: "Load the current number of entries for a running raffle into variables",
        icon: "mdi-cog",
        color: "Info",
        tableName: "subactions_rafflegetentrycount")]
    public class RaffleGetEntryCountType : SubActionType, ISubActionUIProvider
    {
        public RaffleGetEntryCountType() => SubActionTypes = SubActionTypes.RaffleGetEntryCount;

        public string RaffleKey { get; set; } = string.Empty;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new() { PropertyName = nameof(RaffleKey), Label = "Raffle Key", FieldType = UIFieldType.Text, Required = true },
            new() { PropertyName = nameof(Enabled), Label = "Enabled", FieldType = UIFieldType.Switch, SwitchColor = "Success", DefaultValue = true }
        ];

        public Dictionary<string, object?> GetValues() => new()
        {
            [nameof(RaffleKey)] = RaffleKey,
            [nameof(Enabled)] = Enabled
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            RaffleKey = values.GetValueOrDefault(nameof(RaffleKey))?.ToString() ?? string.Empty;
            Enabled = bool.TryParse(values.GetValueOrDefault(nameof(Enabled))?.ToString(), out var enabled) ? enabled : true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            return string.IsNullOrWhiteSpace(values.GetValueOrDefault(nameof(RaffleKey))?.ToString())
                ? "Raffle Key is required"
                : null;
        }
    }
}