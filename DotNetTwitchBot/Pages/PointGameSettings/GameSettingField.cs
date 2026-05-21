namespace DotNetTwitchBot.Pages.PointGameSettings;

public enum GameSettingFieldType
{
    Int,
    Double,
    String,
    Bool
}

public class GameSettingField
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? HelperText { get; set; }
    public GameSettingFieldType FieldType { get; set; }
    /// <summary>Min value for numeric fields. Use int for Int fields, double for Double fields.</summary>
    public object? Min { get; set; }
    /// <summary>Max value for numeric fields. Use int for Int fields, double for Double fields.</summary>
    public object? Max { get; set; }
    public bool Required { get; set; }
}
