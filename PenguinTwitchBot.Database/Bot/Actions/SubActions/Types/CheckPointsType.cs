using PenguinTwitchBot.Bot.Actions.SubActions.UI;


namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Check Points",
        description: "Check a viewer's points of a specific point type.",
        icon: "mdi-check",
        color: "Success",
        tableName: "subactions_checkpoints")]
    public class CheckPointsType : SubActionType, ISubActionUIProvider
    {
        public CheckPointsType() { SubActionTypes = SubActionTypes.CheckPoints; }
        public string PointTypeName { get; set; } = string.Empty;
        public string TargetUser { get; set; } = "%TargetUser%";
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [];
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(PointTypeName), PointTypeName },
                { nameof(TargetUser), TargetUser },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if(values.TryGetValue(nameof(PointTypeName), out var pointTypeName))
                PointTypeName = pointTypeName as string ?? string.Empty;
            if(values.TryGetValue(nameof(TargetUser), out var targetUser))
                TargetUser = targetUser as string ?? string.Empty;
            if(values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? false;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if(!values.TryGetValue(nameof(PointTypeName), out var pointTypeName) || pointTypeName is not string ptName || string.IsNullOrWhiteSpace(ptName))
            {
                return "Point Type is required.";
            }

            if(!values.TryGetValue(nameof(TargetUser), out var targetUser) || targetUser is not string tUser || string.IsNullOrWhiteSpace(tUser))
            {     
                return "Target User is required."; 
            }

            return null;
        }
    }
}
