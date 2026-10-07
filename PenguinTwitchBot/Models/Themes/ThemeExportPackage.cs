namespace PenguinTwitchBot.Models.Themes;

public class ThemeExportPackage
{
    public int Version { get; set; } = 1;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public List<CustomThemeModel> Themes { get; set; } = [];
}
