using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Actions.SubActions
{
    public abstract class SubActionType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [JsonIgnore]
        public int Id { get; set; }
        public int Index { get; set; } = 0;
        public string Text { get; set; } = "";
        public string File { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public SubActionTypes SubActionTypes { get; set; } = SubActionTypes.None;
    }
}
