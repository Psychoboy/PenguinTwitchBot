using DotNetTwitchBot.Bot.Actions.SubActions.UI;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Logic: If/Else",
        description: "Execute subactions based on a condition, if the condition is true, the subactions in the 'If True' section will be executed, if the condition is false, the subactions in the 'If False' section will be executed",
        icon: MdiIcons.CodeBraces,
        color: "Default",
        tableName: "subactions_logic_if_else")]
    public class LogicIfElseType : SubActionType, ISubActionUIProvider
    {
        public string LeftValue { get; set; } = string.Empty;
        public string RightValue { get; set; } = string.Empty;
        public ComparisonOperator Operator { get; set; } = ComparisonOperator.Equals;

        [Column(TypeName = "json")]
        public List<SubActionType> TrueSubActions { get; set; } = [];

        [Column(TypeName = "json")]
        public List<SubActionType> FalseSubActions { get; set; } = [];

        public LogicIfElseType()
        {
            SubActionTypes = SubActionTypes.LogicIfElse;
        }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = "info_hint",
                    Label = "Compare two values and execute different subactions based on the result. Use variables like %user%, %points%, etc.",
                    FieldType = UIFieldType.Info,
                    Severity = "Info",
                    Dense = true
                },
                new()
                {
                    PropertyName = nameof(LeftValue),
                    Label = "First Value",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "The first value to compare (supports variables)"
                },
                new()
                {
                    PropertyName = nameof(Operator),
                    Label = "Comparison Operator",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    Options = Enum.GetNames(typeof(ComparisonOperator))
                },
                new()
                {
                    PropertyName = nameof(RightValue),
                    Label = "Second Value",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "The second value to compare (supports variables)"
                },
                new()
                {
                    PropertyName = "info_subactions",
                    Label = "Note: Configure the True and False subaction lists after creating this subaction.",
                    FieldType = UIFieldType.Info,
                    Severity = "Normal",
                    Dense = true
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
                { nameof(LeftValue), LeftValue },
                { nameof(RightValue), RightValue },
                { nameof(Operator), Operator.ToString() },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(LeftValue), out var leftValue))
                LeftValue = leftValue?.ToString() ?? string.Empty;

            if (values.TryGetValue(nameof(RightValue), out var rightValue))
                RightValue = rightValue?.ToString() ?? string.Empty;

            if (values.TryGetValue(nameof(Operator), out var op) && 
                Enum.TryParse<ComparisonOperator>(op?.ToString(), out var parsedOperator))
                Operator = parsedOperator;

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(LeftValue), out var leftValue) || 
                string.IsNullOrWhiteSpace(leftValue?.ToString()))
            {
                return "First Value is required";
            }

            if (!values.TryGetValue(nameof(RightValue), out var rightValue) || 
                string.IsNullOrWhiteSpace(rightValue?.ToString()))
            {
                return "Second Value is required";
            }

            if (!values.TryGetValue(nameof(Operator), out var op) || 
                !Enum.TryParse<ComparisonOperator>(op?.ToString(), out _))
            {
                return "A valid Comparison Operator is required";
            }

            return null;
        }
    }
}
