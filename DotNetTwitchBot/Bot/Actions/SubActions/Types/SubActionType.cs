using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    public abstract class SubActionType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [JsonIgnore]
        public int Id { get; set; }
        public int Index { get; set; } = 0;
        public string Text { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public SubActionTypes SubActionTypes { get; set; } = SubActionTypes.None;
    }
}
