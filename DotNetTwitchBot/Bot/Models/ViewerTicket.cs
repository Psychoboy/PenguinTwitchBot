namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(Username))]
    public class ViewerTicket
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string Username { get; set; } = "";
        public long Points { get; set; } = 0;
        public bool banned { get; set; } = false;
    }
}