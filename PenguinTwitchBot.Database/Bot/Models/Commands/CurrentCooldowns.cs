using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models.Commands
{
    [ExcludeFromBackup]
    [IndexAttribute(nameof(CommandName), nameof(UserName))]
    [IndexAttribute(nameof(CommandName), nameof(IsGlobal))]
    public class CurrentCooldowns
    {
        [Key]
        public Guid Id { get; set; }
        public bool IsGlobal { get; set; }
        public DateTime NextGlobalCooldownTime { get; set; } = DateTime.MinValue;
        public DateTime NextUserCooldownTime { get; set; } = DateTime.MinValue;
        public string CommandName { get; set; } = null!;
        public string UserName { get; set; } = ""!;
    }
}
