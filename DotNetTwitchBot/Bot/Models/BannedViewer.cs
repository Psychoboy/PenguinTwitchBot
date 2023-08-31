namespace DotNetTwitchBot.Bot.Models
{
    public class BannedViewer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        [Unicode(true)]
        public string Username { get; set; } = "";
        public string Reason { get; set; } = "";
    }
}
