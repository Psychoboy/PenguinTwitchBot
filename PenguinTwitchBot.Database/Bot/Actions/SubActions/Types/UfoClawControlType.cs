using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

[SubActionMetadata(displayName: "UFO Claw Control", description: "Show, hide, grab or manage entries in the UFO claw", icon: "mdi-ufo", color: "Info", category: SubActionCategories.Minigames, tableName: "subactions_ufo_claw_control")]
public sealed class UfoClawControlType : SubActionType, ISubActionUIProvider
{
    public UfoClawControlType() => SubActionTypes = SubActionTypes.UfoClawControl;
    public string Operation { get; set; } = "grab";
    public string Mode { get; set; } = "prizes";
    public string PrizeSetId { get; set; } = "";

    public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
    [
        new() { PropertyName = nameof(Operation), Label = "Operation", FieldType = UIFieldType.Select, Required = true, SelectOptions =
        [
            new() { Value = "show", Name = "Show selected pool" }, new() { Value = "hide", Name = "Hide" },
            new() { Value = "grab", Name = "Grab from current pool" }, new() { Value = "open", Name = "Open fresh viewer entries" },
            new() { Value = "close", Name = "Close viewer entries" }, new() { Value = "reset", Name = "Reset removed winners" }
        ] },
        new() { PropertyName = nameof(Mode), Label = "Show Mode", FieldType = UIFieldType.Select, SelectOptions =
            [new() { Value = "prizes", Name = "Prize set" }, new() { Value = "viewers", Name = "Viewer entries" }] },
        new() { PropertyName = nameof(PrizeSetId), Label = "Prize Set ID", FieldType = UIFieldType.Text, HelperText = "For Show Prize Set. Copy the ID from UFO Claw management. Supports variables." },
        new() { PropertyName = nameof(Enabled), Label = "Enabled", FieldType = UIFieldType.Switch, SwitchColor = "Success" }
    ];

    public Dictionary<string, object?> GetValues() => new() { [nameof(Operation)] = Operation, [nameof(Mode)] = Mode, [nameof(PrizeSetId)] = PrizeSetId, [nameof(Enabled)] = Enabled };
    public void SetValues(Dictionary<string, object?> values)
    {
        if (values.TryGetValue(nameof(Operation), out var operation)) Operation = operation as string ?? "grab";
        if (values.TryGetValue(nameof(Mode), out var mode)) Mode = mode as string ?? "prizes";
        if (values.TryGetValue(nameof(PrizeSetId), out var prizeSet)) PrizeSetId = prizeSet as string ?? "";
        if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
    }
    public string? Validate(Dictionary<string, object?> values)
    {
        var operation = values.GetValueOrDefault(nameof(Operation)) as string ?? Operation;
        var mode = values.GetValueOrDefault(nameof(Mode)) as string ?? Mode;
        if (operation is not ("show" or "hide" or "grab" or "open" or "close" or "reset")) return "Select a valid operation.";
        if (mode is not ("prizes" or "viewers")) return "Select a valid mode.";
        if (operation == "show" && mode == "prizes" && string.IsNullOrWhiteSpace(values.GetValueOrDefault(nameof(PrizeSetId)) as string ?? PrizeSetId)) return "Select a prize set ID.";
        return null;
    }
}