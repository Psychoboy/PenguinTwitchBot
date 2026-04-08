using DotNetTwitchBot.Bot.Actions.SubActions.UI;
using DotNetTwitchBot.Bot.Core.Points;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Check Points",
        description: "Check a viewer's points of a specific point type.",
        icon: MdiIcons.Check,
        color: "Success",
        tableName: "subactions_checkpoints")]
    public class CheckPointsType : SubActionType, ISubActionUIProvider
    {
        public CheckPointsType() { SubActionTypes = SubActionTypes.CheckPoints; }
        public string PointTypeName { get; set; } = string.Empty;
        public string TargetUser { get; set; } = "%TargetUser%";
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            if (serviceProvider == null)
            {
                return [];
            }

            using var scope = serviceProvider.CreateScope();
            var pointSystem = scope.ServiceProvider.GetRequiredService<IPointsSystem>();
            var pointTypes = Task.Run(async () => await pointSystem.GetPointTypes()).GetAwaiter().GetResult();
            var pointTypeNames = pointTypes.Select(pt => pt.Name).ToArray();

            return [
                new()
                {
                    PropertyName = nameof(PointTypeName),
                    Label = "Point Type",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    Options = pointTypeNames
                },
                new()
                {
                    PropertyName = nameof(TargetUser),
                    Label = "Target User",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "The user to check points for. You can use variables here, such as %TargetUser% or %User%."
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                },
                new()
                {
                    PropertyName = "Info1",
                    Label = "Info",
                    FieldType = UIFieldType.Info,
                    DefaultValue = "This will check targets points and populate %TargetPoints% and %TargetPointsFormatted%."
                }
            ];
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
