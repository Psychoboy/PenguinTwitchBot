namespace PenguinTwitchBot.Database.Bot.Models.Overlay
{
    /// <summary>
    /// Describes a single available overlay widget type.
    /// Add new entries here to make additional widgets available in the editor.
    /// </summary>
    public record WidgetDefinition(
        string Type,
        string DisplayName,
        string SourcePath,
        int DefaultWidth,
        int DefaultHeight
    );

    /// <summary>
    /// Central registry of all overlay widget types known to the system.
    /// To add a new widget (e.g. a Twitch chat widget), append a new WidgetDefinition here.
    /// </summary>
    public static class WidgetRegistry
    {
        public static readonly IReadOnlyList<WidgetDefinition> All =
        [
            new("alerts",    "Alerts",    "/alerts.html",    1920, 1080),
            new("clips",     "Clips",     "/clips.html",     1920, 1080),
            new("fireworks", "Fireworks", "/fireworks.html", 1920, 1080),
            new("fishing",   "Fishing",   "/fishing.html",    500,  300),
            new("fishing_tournaments", "Fishing Tournaments", "/fishing-tournaments.html", 620, 380),
            new("wheel",     "Wheel",     "/wheel.html",      1920,  1080),
            new("chat",      "Chat",      "/chat.html",       400,  600),
        ];

        public static WidgetDefinition? Find(string type) =>
            All.FirstOrDefault(w => w.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
    }
}
