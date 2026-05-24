using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Raffle Set Total Award",
        description: "Update the total award for a running raffle",
        icon: MdiIcons.Cog,
        color: "Primary",
        tableName: "subactions_rafflesettotalaward")]
    public class RaffleSetTotalAwardType : SubActionType, ISubActionUIProvider
    {
        public RaffleSetTotalAwardType() => SubActionTypes = SubActionTypes.RaffleSetTotalAward;

        public string RaffleKey { get; set; } = string.Empty;
        public long TotalAward { get; set; }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new() { PropertyName = nameof(RaffleKey), Label = "Raffle Key", FieldType = UIFieldType.Text, Required = true },
            new() { PropertyName = nameof(TotalAward), Label = "Total Award", FieldType = UIFieldType.Number, Required = true, DefaultValue = 0, Min = 0 },
            new() { PropertyName = nameof(Enabled), Label = "Enabled", FieldType = UIFieldType.Switch, SwitchColor = "Success", DefaultValue = true }
        ];

        public Dictionary<string, object?> GetValues() => new()
        {
            [nameof(RaffleKey)] = RaffleKey,
            [nameof(TotalAward)] = TotalAward,
            [nameof(Enabled)] = Enabled
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            RaffleKey = values.GetValueOrDefault(nameof(RaffleKey))?.ToString() ?? string.Empty;
            TotalAward = long.TryParse(values.GetValueOrDefault(nameof(TotalAward))?.ToString(), out var totalAward) ? totalAward : 0;
            Enabled = bool.TryParse(values.GetValueOrDefault(nameof(Enabled))?.ToString(), out var enabled) ? enabled : true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(nameof(RaffleKey))?.ToString()))
            {
                return "Raffle Key is required";
            }

            return long.TryParse(values.GetValueOrDefault(nameof(TotalAward))?.ToString(), out var totalAward) && totalAward >= 0
                ? null
                : "Total Award must be 0 or greater";
        }
    }
}