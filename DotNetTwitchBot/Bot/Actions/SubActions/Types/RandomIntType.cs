using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Random Number",
        description: "Generate a random number for use in actions",
        icon: MdiIcons.DiceMultiple,
        color: "Warning",
        tableName: "subactions_randomint")]
    public class RandomIntType : SubActionType, ISubActionUIProvider
    {
        public RandomIntType()
        {
            SubActionTypes = SubActionTypes.RandomInt;
        }

        public int Min { get; set; } = 0;
        public int Max { get; set; } = 100;

        public List<SubActionUIField> GetUIFields()
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(Min),
                    Label = "Minimum Value",
                    FieldType = UIFieldType.Number
                },
                new()
                {
                    PropertyName = nameof(Max),
                    Label = "Maximum Value",
                    FieldType = UIFieldType.Number
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    Attributes = new Dictionary<string, object> { { "Color", "Success" } }
                }
            };
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(Min), Min },
                { nameof(Max), Max },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Min), out var min))
                Min = min as int? ?? 0;
            if (values.TryGetValue(nameof(Max), out var max))
                Max = max as int? ?? 100;
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            var min = values.TryGetValue(nameof(Min), out var minObj) ? (minObj as int? ?? 0) : 0;
            var max = values.TryGetValue(nameof(Max), out var maxObj) ? (maxObj as int? ?? 100) : 100;

            if (max <= min)
                return "Maximum value must be greater than minimum value";
            return null;
        }
    }
}
