using DotNetTwitchBot.Bot.Actions.SubActions.UI;
using DotNetTwitchBot.Bot.Commands.PastyGames;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Multi Counter",
        description: "Updates and Return counter with specific name",
        icon: MdiIcons.Counter,
        color: "Info",
        tableName: "subactions_multicounter")]
    public class MultiCounterType : SubActionType, ISubActionUIProvider
    {
        public MultiCounterType() { SubActionTypes = SubActionTypes.MultiCounter; }
        public int? Min { get; set; } = 0;
        public int? Max { get; set; } = 100;
        public string Name { get; set; } = "";
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new List<SubActionUIField>
            {
                 new()
                {
                    PropertyName = nameof(Name),
                    Label = "Counter Name",
                    FieldType = UIFieldType.TextArea,
                    Required = true,
                    HelperText = "Set the counter name. Will populate variable %counter_NAMEHERE%",
                    Lines = 1
                },
                new()
                {
                    PropertyName = nameof(Min),
                    Label = "Minimum Value",
                    FieldType = UIFieldType.Number,
                    Clearable = true
                },
                new()
                {
                    PropertyName = nameof(Max),
                    Label = "Maximum Value",
                    FieldType = UIFieldType.Number,
                    Clearable = true
                },
                new()
                {
                    PropertyName = "info_hint",
                    Label = "The result is available as %counter_NAMEHERE% variable.",
                    FieldType = UIFieldType.Info,
                    Severity = "Info",
                    Dense = true
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

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(Name), Name },
                { nameof(Min), Min },
                { nameof(Max), Max },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if(values.TryGetValue(nameof(Name), out var name))
                Name = name as string ?? "";
            if (values.TryGetValue(nameof(Min), out var min))
                Min = min as int?;
            if (values.TryGetValue(nameof(Max), out var max))
                Max = max as int?;
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Name), out var nameObj))
            {
                var name = nameObj as string ?? "";
                if (string.IsNullOrWhiteSpace(name))
                    return "Counter Name cannot be empty.";
                if (name.Contains(" "))
                    return "Counter Name cannot contain spaces.";
            }
            else
            {
                return "Counter Name is required.";
            }
            return null;
        }
    }
}
