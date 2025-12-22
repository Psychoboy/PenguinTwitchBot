namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(UserId), IsUnique = true)]
    public class ScAiResponseCodes
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string UserId { get; set; } = string.Empty;
        public string PreviousResponseId { get; set; } = string.Empty;
    }
}
