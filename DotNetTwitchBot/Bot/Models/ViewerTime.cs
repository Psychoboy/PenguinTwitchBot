namespace DotNetTwitchBot.Bot.Models
{
    public class ViewerTime
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string Username { get; set; } = "";
        public long Time { get; set; } = 0;
        public bool banned { get; set; } = false;
    }
}