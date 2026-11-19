using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PenguinTwitchBot.Database.Bot.Models.Credits
{
    [Table("CreditProfiles")]
    [Index(nameof(Name), IsUnique = true)]
    public class CreditProfile
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = "Default";

        public bool IsDefault { get; set; }

        public Guid? ThemeId { get; set; }

        public CreditTheme? Theme { get; set; }

        [Required]
        public string ConfigJson { get; set; } = "{}";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
