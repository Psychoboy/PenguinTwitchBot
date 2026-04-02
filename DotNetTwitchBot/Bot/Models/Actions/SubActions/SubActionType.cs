using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Actions.SubActions
{
    public class SubActionType
    {
        [Key]
        [JsonIgnore]
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Index { get; set; } = 0;
        public string Text { get; set; } = "";
        public string File { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public SubActionTypes SubActionTypes { get; set; } = SubActionTypes.None;
    }
}
