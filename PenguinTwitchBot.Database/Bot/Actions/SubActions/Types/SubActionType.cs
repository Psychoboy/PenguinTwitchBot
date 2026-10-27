using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    public abstract class SubActionType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid Id { get; set; }
        public Guid? ActionTypeId { get; set; }
        public Guid? CatchActionTypeId { get; set; }
        public int Index { get; set; } = 0;
        public string Text { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public SubActionTypes SubActionTypes { get; set; } = SubActionTypes.None;
    }
}
