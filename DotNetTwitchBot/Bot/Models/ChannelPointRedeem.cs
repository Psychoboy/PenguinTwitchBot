namespace DotNetTwitchBot.Bot.Models
{
    public class ChannelPointRedeem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public Rank ElevatedPermission { get; set; } = Rank.Viewer;
    }
}
