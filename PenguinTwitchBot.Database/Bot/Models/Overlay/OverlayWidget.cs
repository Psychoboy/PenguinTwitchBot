using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models.Overlay
{
    [Table("overlay_widgets")]
    public class OverlayWidget
    {
        [Key]
        public int Id { get; set; }

        public int OverlayLayoutId { get; set; }

        [JsonIgnore]
        public OverlayLayout Layout { get; set; } = null!;

        /// <summary>
        /// The widget type key — matches a WidgetDefinition.Type in the WidgetRegistry.
        /// e.g. "alerts", "clips", "fireworks", "fishing", "wheel"
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string WidgetType { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public bool IsLocked { get; set; } = false;

        /// <summary>X position in pixels on a 1920x1080 canvas.</summary>
        public int X { get; set; } = 0;

        /// <summary>Y position in pixels on a 1920x1080 canvas.</summary>
        public int Y { get; set; } = 0;

        public int Width { get; set; } = 1920;

        public int Height { get; set; } = 1080;

        public int ZIndex { get; set; } = 1;

        /// <summary>
        /// Reserved for Phase 4: JSON blob of per-widget CSS variable overrides.
        /// </summary>
        public string? CustomSettings { get; set; }
    }
}
