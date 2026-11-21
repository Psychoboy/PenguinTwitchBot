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
        public bool WaitForReveal { get; set; } = true;

        public override List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(Attempts),
                    Label = "Fishing Attempts",
                    FieldType = UIFieldType.Text,
                    Required = false,
                    DefaultValue = "1",
                    HelperText = "Number of fishing attempts (1-15) or a variable (e.g., %attempts%)."
                },
                new()
                {
                    PropertyName = nameof(WaitForReveal),
                    Label = "Wait for Cast Reveal",
                    FieldType = UIFieldType.Switch,
                    DefaultValue = true,
                    SwitchColor = "Info",
                    HelperText = "When a registered overlay with the secure key is connected, rewards and triggers automatically sync per catch as each fish is displayed. Enable this to also add a fallback cast delay when no registered overlay is connected."
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
            values[nameof(Attempts)] = !string.IsNullOrWhiteSpace(Text) ? Text : Attempts.ToString();
            values[nameof(WaitForReveal)] = WaitForReveal;
        }

        protected override void SetCustomValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Attempts), out var attempts))
            {
                var raw = attempts?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    Attempts = 1;
                    Text = string.Empty;
                }
                else if (int.TryParse(raw, out var parsed))
                {
                    Attempts = Math.Clamp(parsed, 1, 15);
                    Text = string.Empty;
                }
                else
                {
                    // Variable expression (e.g. %attempts%) resolved at runtime by FishingHandler
                    Text = raw;
                    Attempts = 1;
                }
            }
            if (values.TryGetValue(nameof(WaitForReveal), out var waitForReveal))
            {
                WaitForReveal = waitForReveal as bool? ?? true;
            }
        }

        protected override string? ValidateCustom(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Attempts), out var attempts) && attempts != null)
            {
                var raw = attempts.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                // Allow variable placeholders like %attempts%
                if (raw.Contains('%'))
                {
                    return null;
                }

                if (!int.TryParse(raw, out var attemptsValue))
                    return "Attempts must be a number (1-15) or a variable like %attempts%";

                if (attemptsValue < 1 || attemptsValue > 15)
                    return "Attempts must be between 1 and 15";
            }
            return null;
        }
    }
}
