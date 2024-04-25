namespace DotNetTwitchBot.Bot.Models
{
    public class DiscordEventMap
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }

        public ulong DiscordEventId { get; set; }
        public string TwitchEventId { get; set; } = "";
    }
}
