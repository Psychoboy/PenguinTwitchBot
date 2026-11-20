using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Fishing",
        description: "Perform fishing attempts with chance to catch different fish",
        icon: "mdi-fish",
        color: "Primary",
        tableName: "subactions_fishing")]
    public class FishingType : SimpleSubActionType
    {
        public FishingType() { SubActionTypes = SubActionTypes.Fishing; }

        public int Attempts { get; set; } = 1;
        public bool WaitForReveal { get; set; } = false;

        public override List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(Attempts),
                    Label = "Fishing Attempts",
                    FieldType = UIFieldType.Number,
                    Required = false,
                    Min = 1,
                    Max = 15,
                    DefaultValue = 1,
                    HelperText = "Number of fishing attempts to perform (1-15)"
                },
                new()
                {
                    PropertyName = nameof(WaitForReveal),
                    Label = "Wait for Cast Reveal",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Info",
                    HelperText = "Pause briefly (~1.6s) for the cast/bite animation so chat & awards land right as the fish splashes onto the overlay."
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            };
        }

        protected override void AddCustomValues(Dictionary<string, object?> values)
        {
            values[nameof(Attempts)] = Attempts;
            values[nameof(WaitForReveal)] = WaitForReveal;
        }

        protected override void SetCustomValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Attempts), out var attempts))
            {
                if (attempts is int attemptsInt)
                    Attempts = attemptsInt;
                else if (attempts is long attemptsLong)
                    Attempts = (int)attemptsLong;
                else if (attempts != null && int.TryParse(attempts.ToString(), out var parsed))
                    Attempts = parsed;
            }
            if (values.TryGetValue(nameof(WaitForReveal), out var waitForReveal))
            {
                WaitForReveal = waitForReveal as bool? ?? false;
            }
        }

        protected override string? ValidateCustom(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Attempts), out var attempts))
            {
                if (attempts != null)
                {
                    var attemptsValue = 0;
                    if (attempts is int attemptsInt)
                        attemptsValue = attemptsInt;
                    else if (attempts is long attemptsLong)
                        attemptsValue = (int)attemptsLong;
                    else if (!int.TryParse(attempts.ToString(), out attemptsValue))
                        return "Attempts must be a valid number";

                    if (attemptsValue < 1 || attemptsValue > 15)
                        return "Attempts must be between 1 and 15";
                }
            }
            return null;
        }
    }
}
