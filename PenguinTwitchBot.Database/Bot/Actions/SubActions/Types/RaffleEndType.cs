using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Raffle End",
        description: "Close a raffle, pick winners, and award the configured total",
        icon: "mdi-cog",
        color: "Warning",
        tableName: "subactions_raffleend")]
    public class RaffleEndType : SubActionType, ISubActionUIProvider
    {
        public RaffleEndType() => SubActionTypes = SubActionTypes.RaffleEnd;

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