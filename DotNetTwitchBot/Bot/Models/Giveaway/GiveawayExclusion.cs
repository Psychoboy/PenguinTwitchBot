namespace DotNetTwitchBot.Bot.Models.Giveaway
{
    public class GiveawayExclusion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string Username { get; set; } = null!;
        public DateTime? ExpireDateTime { get; set; }
        public string? Reason { get; set; }
    }
}
