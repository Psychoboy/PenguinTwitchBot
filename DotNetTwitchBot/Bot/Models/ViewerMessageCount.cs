namespace DotNetTwitchBot.Bot.Models
{
    public class ViewerMessageCount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string Username { get; set; } = "";
        public long MessageCount { get; set; } = 0;
        public bool banned { get; set; } = false;
    }
}