using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    public enum ManagePointsOperation
    {
        Add,
        Remove,
        Set,
        AddActive
    }

    [SubActionMetadata(
        displayName: "Manage Points",
        description: "Add, remove, or set points for a viewer, or award active viewers",
        icon: "mdi-coins",
        color: "Warning",
        tableName: "subactions_managepoints",
        category: SubActionCategories.PointsAndRewards)]
    public class ManagePointsType : SubActionType, ISubActionUIProvider
    {
        public ManagePointsType() => SubActionTypes = SubActionTypes.ManagePoints;

        public string PointTypeName { get; set; } = string.Empty;
        public string Operation { get; set; } = nameof(ManagePointsOperation.Add);
        public string TargetUser { get; set; } = "%User%";
        public string Amount { get; set; } = "%Score%";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new()
            {
                PropertyName = nameof(PointTypeName),
                Label = "Point Type",
                FieldType = UIFieldType.Select,
                Required = true,
                Options = []
            },
            new()
            {
                PropertyName = nameof(Operation),
                Label = "Operation",
                FieldType = UIFieldType.Select,
                Required = true,
                Options = Enum.GetNames<ManagePointsOperation>()
            },
            new()
            {
                PropertyName = nameof(TargetUser),
                Label = "Target User",
                FieldType = UIFieldType.Text,
                HelperText = "Used for Add, Remove, and Set. Defaults to the triggering user; AddActive ignores this field."
            },
            new()
            {
                PropertyName = nameof(Amount),
                Label = "Amount / Formula",
                FieldType = UIFieldType.Text,
                Required = true,
                HelperText = "Supports variables and $math(... )$ expressions, for example %Score% or $math(%Score% * 2)$"
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
            [nameof(PointTypeName)] = PointTypeName,
            [nameof(Operation)] = Operation,
            [nameof(TargetUser)] = TargetUser,
            [nameof(Amount)] = Amount,
            [nameof(Enabled)] = Enabled
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(PointTypeName), out var pointType) && pointType is string pointTypeName)
                PointTypeName = pointTypeName;
            if (values.TryGetValue(nameof(Operation), out var operation) && operation is string operationName)
                Operation = operationName;
            if (values.TryGetValue(nameof(TargetUser), out var target) && target is string targetUser)
                TargetUser = targetUser;
            if (values.TryGetValue(nameof(Amount), out var amount) && amount is string amountText)
                Amount = amountText;
            if (values.TryGetValue(nameof(Enabled), out var enabled) && enabled is bool isEnabled)
                Enabled = isEnabled;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(PointTypeName), out var pointType) || pointType is not string pointTypeName || string.IsNullOrWhiteSpace(pointTypeName))
                return "Point type is required.";
            if (!values.TryGetValue(nameof(Operation), out var operation) || operation is not string operationName ||
                !Enum.TryParse<ManagePointsOperation>(operationName, true, out _))
                return "Select a valid points operation.";
            if (!values.TryGetValue(nameof(Amount), out var amount) || amount is not string amountText || string.IsNullOrWhiteSpace(amountText))
                return "Amount or formula is required.";
            if (!operationName.Equals("AddActive", StringComparison.OrdinalIgnoreCase) &&
                (!values.TryGetValue(nameof(TargetUser), out var target) || target is not string targetUser || string.IsNullOrWhiteSpace(targetUser)))
                return "Target user is required for this operation.";
            return null;
        }
    }
}