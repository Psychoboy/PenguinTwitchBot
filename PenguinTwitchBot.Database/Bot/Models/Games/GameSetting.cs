using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models.Games
{
    [IndexAttribute(nameof(GameName))]
    [IndexAttribute(nameof(SettingName))]
    [IndexAttribute(nameof(GameName),nameof(SettingName))]
    public class GameSetting
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string GameName { get; set; } = null!;
        public string SettingName { get; set; } = null!;
        public string? SettingStringValue { get; set; }
        public int SettingIntValue { get; set; }
        public bool SettingBoolValue { get; set; }
        public double SettingDoubleValue { get; set; }
        public long SettingLongValue { get; set; }
    }
}
