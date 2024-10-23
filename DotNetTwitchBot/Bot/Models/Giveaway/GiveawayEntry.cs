namespace DotNetTwitchBot.Bot.Models.Giveaway
{
    [Index(nameof(Username), IsUnique = true)]
    public class GiveawayEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Int64? Id { get; set; }
        public string Username { get; set; } = "";
        public string UserId { get; set; } = string.Empty;
        public Int32 Tickets { get; set; } = 0;
    }
}