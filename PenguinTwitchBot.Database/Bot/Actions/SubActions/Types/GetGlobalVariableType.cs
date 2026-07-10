using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Get Global Variable",
        description: "Copy a persistent global variable into a local action variable for use with %variable% replacements.",
        icon: "mdi-database-arrow-down",
        color: "Secondary",
        tableName: "subactions_getglobalvariable")]
    public class GetGlobalVariableType : SimpleSubActionType
    {
        public GetGlobalVariableType()
        {
            SubActionTypes = SubActionTypes.GetGlobalVariable;
        }

        public string TargetVariableName { get; set; } = string.Empty;

        protected override string TextLabel => "Global Variable Name";
        protected override string TextHelperText => "Use the global variable name directly, without % signs.";
        protected override bool TextRequired => true;

        protected override void AddCustomFields(List<SubActionUIField> fields)
        {
            fields.Add(new SubActionUIField
            {
                PropertyName = nameof(TargetVariableName),
                Label = "Local Variable Name",
                FieldType = UIFieldType.Text,
                Required = true,
                HelperText = "The value will be copied into this action variable."
            });
        }

        protected override void AddCustomValues(Dictionary<string, object?> values)
        {
            values[nameof(TargetVariableName)] = TargetVariableName;
        }

        protected override void SetCustomValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(TargetVariableName), out var targetVariableName))
                TargetVariableName = targetVariableName as string ?? string.Empty;
        }

        protected override string? ValidateCustom(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(TargetVariableName), out var targetVariableName) || string.IsNullOrWhiteSpace(targetVariableName as string))
                return "Local variable name is required.";

            return null;
        }
    }
}