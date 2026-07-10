using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Set Global Variable",
        description: "Store a value in a persistent global variable that survives between action executions and backups.",
        icon: "mdi-database-arrow-up",
        color: "Secondary",
        tableName: "subactions_setglobalvariable")]
    public class SetGlobalVariableType : SimpleSubActionType
    {
        public SetGlobalVariableType()
        {
            SubActionTypes = SubActionTypes.SetGlobalVariable;
        }

        public string Value { get; set; } = string.Empty;

        protected override string TextLabel => "Global Variable Name";
        protected override string TextHelperText => "Use the variable name directly, without % signs.";
        protected override bool TextRequired => true;

        protected override void AddCustomFields(List<SubActionUIField> fields)
        {
            fields.Add(new SubActionUIField
            {
                PropertyName = nameof(Value),
                Label = "Value",
                FieldType = UIFieldType.TextArea,
                Lines = 3,
                Required = true,
                HelperText = "Use %localVariable% to pull from the current action variables."
            });
        }

        protected override void AddCustomValues(Dictionary<string, object?> values)
        {
            values[nameof(Value)] = Value;
        }

        protected override void SetCustomValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Value), out var value))
                Value = value as string ?? string.Empty;
        }

        protected override string? ValidateCustom(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(Value), out var value) || string.IsNullOrWhiteSpace(value as string))
                return "Value is required.";

            return null;
        }
    }
}