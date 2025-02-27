using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Games
{
    [Index(nameof(GameName))]
    [Index(nameof(SettingName))]
    [Index(nameof(GameName),nameof(SettingName))]
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
    }
}
