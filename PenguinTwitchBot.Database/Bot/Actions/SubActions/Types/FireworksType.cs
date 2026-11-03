using System.Globalization;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Fireworks",
        description: "Launches fireworks on the Fireworks overlay widget; requires that widget in an overlay layout",
        icon: "mdi-firework",
        color: "Warning",
        tableName: "subactions_fireworks",
        category: SubActionCategories.ChatAndMedia)]
    public class FireworksType : SubActionType, ISubActionUIProvider
    {
        public FireworksType() => SubActionTypes = SubActionTypes.Fireworks;

        public string Count { get; set; } = "1";
        public string DelayMilliseconds { get; set; } = "100";
        public string StartDelayMilliseconds { get; set; } = "0";
        public string DelayVarianceMilliseconds { get; set; } = "0";
        public string Shell { get; set; } = "";
        public string Size { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new()
            {
                PropertyName = nameof(Count),
                Label = "Burst Count",
                FieldType = UIFieldType.Text,
                Required = true,
                HelperText = "Number of bursts (1-100). Supports %variables% and $math(expression)$; for example, $math(%Amount% + 1)$."
            },
            new()
            {
                PropertyName = nameof(StartDelayMilliseconds),
                Label = "Delay Before First Burst (ms)",
                FieldType = UIFieldType.Text,
                Required = true,
                HelperText = "Pause before this sub-action's first burst (0-60000). Supports %variables% and $math(expression)$; useful between show phases."
            },
            new()
            {
                PropertyName = nameof(DelayMilliseconds),
                Label = "Delay Between Bursts (ms)",
                FieldType = UIFieldType.Text,
                Required = true,
                HelperText = "Delay between bursts in milliseconds (0-60000). Supports %variables% and $math(expression)$."
            },
            new()
            {
                PropertyName = nameof(DelayVarianceMilliseconds),
                Label = "Delay Variance (±ms)",
                FieldType = UIFieldType.Text,
                Required = true,
                HelperText = "Randomly varies each between-burst delay by up to this amount (0-60000). Supports %variables% and $math(expression)$; negative results are clamped to 0."
            },
            new()
            {
                PropertyName = nameof(Shell),
                Label = "Shell Override (Optional)",
                FieldType = UIFieldType.Text,
                HelperText = "Leave blank to use the Fireworks widget setting. Supports %variables%."
            },
            new()
            {
                PropertyName = nameof(Size),
                Label = "Size Override (Optional)",
                FieldType = UIFieldType.Text,
                HelperText = "Leave blank to use the Fireworks widget setting. Use 0-5; supports %variables%."
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
            { nameof(Count), Count },
            { nameof(DelayMilliseconds), DelayMilliseconds },
            { nameof(StartDelayMilliseconds), StartDelayMilliseconds },
            { nameof(DelayVarianceMilliseconds), DelayVarianceMilliseconds },
            { nameof(Shell), Shell },
            { nameof(Size), Size },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Count), out var count)) Count = count as string ?? "1";
            if (values.TryGetValue(nameof(DelayMilliseconds), out var delay)) DelayMilliseconds = delay as string ?? "100";
            if (values.TryGetValue(nameof(StartDelayMilliseconds), out var startDelay)) StartDelayMilliseconds = startDelay as string ?? "0";
            if (values.TryGetValue(nameof(DelayVarianceMilliseconds), out var variance)) DelayVarianceMilliseconds = variance as string ?? "0";
            if (values.TryGetValue(nameof(Shell), out var shell)) Shell = shell as string ?? "";
            if (values.TryGetValue(nameof(Size), out var size)) Size = size as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            var startDelayError = ValidateIntegerTemplate(values, nameof(StartDelayMilliseconds), 0, 60000, "Start delay");
            if (startDelayError != null) return startDelayError;
            var delayError = ValidateIntegerTemplate(values, nameof(DelayMilliseconds), 0, 60000, "Delay between bursts");
            if (delayError != null) return delayError;
            var varianceError = ValidateIntegerTemplate(values, nameof(DelayVarianceMilliseconds), 0, 60000, "Delay variance");
            if (varianceError != null) return varianceError;

            if (!values.TryGetValue(nameof(Size), out var sizeValue) || sizeValue == null)
            {
                return null;
            }

            var size = sizeValue switch
            {
                string text => text.Trim(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? "",
                _ => sizeValue.ToString()?.Trim() ?? ""
            };

            if (string.IsNullOrWhiteSpace(size) || size.Contains('%') || size.Contains("$math(", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!double.TryParse(size, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                !double.IsFinite(parsed) || parsed < 0 || parsed > 5)
            {
                return "Size must be blank or resolve to a number between 0 and 5.";
            }

            return null;
        }

        private static string? ValidateIntegerTemplate(
            Dictionary<string, object?> values,
            string propertyName,
            int min,
            int max,
            string label)
        {
            if (!values.TryGetValue(propertyName, out var value) || value == null) return null;
            var text = value switch
            {
                string stringValue => stringValue.Trim(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? "",
                _ => value.ToString()?.Trim() ?? ""
            };
            if (text.Contains('%') || text.Contains("$math(", StringComparison.OrdinalIgnoreCase)) return null;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < min || parsed > max)
                return $"{label} must resolve to a whole number between {min} and {max}.";
            return null;
        }
    }
}