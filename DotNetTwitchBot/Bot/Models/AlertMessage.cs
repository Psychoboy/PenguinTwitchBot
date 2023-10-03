namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(Name))]
    public class AlertMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }

        [Required]
        public string Name { get; set; } = null!;

        public string Value { get; set; } = string.Empty;
    }
}
