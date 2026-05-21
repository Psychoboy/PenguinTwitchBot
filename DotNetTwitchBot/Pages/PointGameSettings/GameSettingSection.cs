namespace DotNetTwitchBot.Pages.PointGameSettings;

public class GameSettingSection
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public List<GameSettingField> Fields { get; set; } = new();
    /// <summary>1 = full-width stack, 2 = two-column grid.</summary>
    public int Columns { get; set; } = 1;
}
