using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Timers
{
    public class TimerMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string Message {get;set;} = null!;
        public bool Enabled {get;set;} = true;

        [JsonIgnore]
        public int TimerGroupId { get; set; }
        [JsonIgnore]
        public TimerGroup TimerGroup { get; set; } = null!;
    }
}