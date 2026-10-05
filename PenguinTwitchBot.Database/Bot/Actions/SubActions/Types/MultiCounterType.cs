using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    public enum CounterOperation
    {
        CommandArgs,
        Increment,
        Decrement,
        Reset,
        Set,
        Get
    }

    [SubActionMetadata(
        displayName: "Counter",
        description: "Updates and returns counter value, supporting command parameters and permissions",
        icon: "mdi-counter",
        color: "Info",
        tableName: "subactions_multicounter")]
    public class MultiCounterType : SubActionType, ISubActionUIProvider
    {
        public MultiCounterType() { SubActionTypes = SubActionTypes.MultiCounter; }
        public int? Min { get; set; } = 0;
        public int? Max { get; set; } = 100;
        public string Name { get; set; } = "";
        public CounterOperation Operation { get; set; } = CounterOperation.CommandArgs;
        public int? Value { get; set; }
        public string? DestinationVariable { get; set; }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(Name),
                    Label = "Counter Name",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "Select or type the counter name. Will populate variable %counter_NAME% and %counter_value%"
                },
                new()
                {
                    PropertyName = nameof(Operation),
                    Label = "Operation",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions =
                    [
                        new() { Name = "Evaluate from Command Arguments (+, -, reset, set)", Value = nameof(CounterOperation.CommandArgs) },
                        new() { Name = "Increment (+)", Value = nameof(CounterOperation.Increment) },
                        new() { Name = "Decrement (-)", Value = nameof(CounterOperation.Decrement) },
                        new() { Name = "Reset", Value = nameof(CounterOperation.Reset) },
                        new() { Name = "Set to Value", Value = nameof(CounterOperation.Set) },
                        new() { Name = "Get / Read Value Only", Value = nameof(CounterOperation.Get) }
                    ],
                    HelperText = "Choose how to update or read the counter."
                },
                new()
                {
                    PropertyName = nameof(Value),
                    Label = "Value / Amount",
                    FieldType = UIFieldType.Number,
                    Clearable = true,
                    HelperText = "Optional amount to add/subtract or set to. If blank, uses counter's default step."
                },
                new()
                {
                    PropertyName = nameof(DestinationVariable),
                    Label = "Destination Variable (Optional)",
                    FieldType = UIFieldType.Text,
                    Clearable = true,
                    HelperText = "Optional custom variable name (without % signs) to save the resulting count into."
                },
                new()
                {
                    PropertyName = nameof(Min),
                    Label = "Minimum Value (Override)",
                    FieldType = UIFieldType.Number,
                    Clearable = true,
                    HelperText = "Optional minimum bound override."
                },
                new()
                {
                    PropertyName = nameof(Max),
                    Label = "Maximum Value (Override)",
                    FieldType = UIFieldType.Number,
                    Clearable = true,
                    HelperText = "Optional maximum bound override."
                },
                new()
                {
                    PropertyName = "info_hint",
                    Label = "Populates %counter_NAME%, %counter_value%, %counter_old_value%, %counter_operation%, and %counter_success% for downstream subactions.",
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
                { nameof(Operation), Operation.ToString() },
                { nameof(Value), Value },
                { nameof(DestinationVariable), DestinationVariable },
                { nameof(Min), Min },
                { nameof(Max), Max },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Name), out var name))
                Name = name as string ?? "";
            if (values.TryGetValue(nameof(Operation), out var opObj))
            {
                if (opObj is CounterOperation op)
                    Operation = op;
                else if (opObj is string opStr && Enum.TryParse<CounterOperation>(opStr, true, out var parsedOp))
                    Operation = parsedOp;
            }
            if (values.TryGetValue(nameof(Value), out var val))
            {
                if (val is int intVal)
                    Value = intVal;
                else if (val != null && int.TryParse(val.ToString(), out var parsedVal))
                    Value = parsedVal;
                else
                    Value = null;
            }
            if (values.TryGetValue(nameof(DestinationVariable), out var destVar))
                DestinationVariable = destVar as string;
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
                if (name.Contains(' '))
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
