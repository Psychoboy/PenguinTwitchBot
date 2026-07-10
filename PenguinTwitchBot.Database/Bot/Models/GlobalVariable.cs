using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models
{
    [IndexAttribute(nameof(Name), IsUnique = true)]
    public class GlobalVariable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }
}