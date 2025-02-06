using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models
{
    public class RegisteredVoice
    {
        public enum VoiceType
        {
            Windows,
            Google
        }

        public enum SexType
        {
            None,
            Male,
            Female,
            Neutral
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public VoiceType Type { get; set; }
        public string Name { get; set; } = "";
        public string? LanguageCode { get; set; }
        public SexType Sex { get; set; } = SexType.None;
    }
}
