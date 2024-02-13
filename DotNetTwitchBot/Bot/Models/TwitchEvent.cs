using DotNetTwitchBot.Bot.Commands.TwitchEvents;

namespace DotNetTwitchBot.Bot.Models
{
    public class TwitchEvent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public TwitchEventType EventType { get; set; }
        public string Command { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Rank ElevatedPermission { get; set; } = Rank.Streamer;
    }
}
