using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PenguinTwitchBot.Database.Bot.Models.Credits
{
    [Table("CreditThemes")]
    [Index(nameof(Name), IsUnique = true)]
    [Index(nameof(BuiltInKey))]
    public class CreditTheme
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsBuiltIn { get; set; }

        [MaxLength(64)]
        public string? BuiltInKey { get; set; }

        [Required]
        public string ThemeJson { get; set; } = "{}";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
