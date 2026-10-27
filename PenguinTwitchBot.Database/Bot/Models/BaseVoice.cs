using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models
{
    public abstract class BaseVoice
    {
        public enum VoiceType
        {
            Windows,
            Google,
            Kokoro,
            Piper
        }

        public enum SexType
        {
            None,
            Male,
            Female,
            Neutral
        }

        [Key]
        public Guid Id { get; set; }
        public VoiceType Type { get; set; }
        public string Name { get; set; } = "";
        public string? LanguageCode { get; set; }
        public SexType Sex { get; set; } = SexType.None;
    }
}

