namespace DotNetTwitchBot.Bot.Models.Timers
{
    public class TimerMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string Message {get;set;} = null!;
        public bool Enabled {get;set;} = true;
    }
}