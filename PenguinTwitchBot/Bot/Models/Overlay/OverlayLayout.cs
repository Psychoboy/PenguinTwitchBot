using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PenguinTwitchBot.Bot.Models.Overlay
{
    [Table("overlay_layouts")]
    [Index(nameof(Name), IsUnique = true)]
    public class OverlayLayout
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsDefault { get; set; } = false;

        public int CanvasWidth { get; set; } = 1920;

        public int CanvasHeight { get; set; } = 1080;

        public List<OverlayWidget> Widgets { get; set; } = [];
    }
}
