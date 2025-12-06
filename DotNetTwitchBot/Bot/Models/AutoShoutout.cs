using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(Name))]
    public class AutoShoutout
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string Name {get;set;} = null!;
        public string? CustomMessage {get;set;}
        public DateTime LastShoutout {get;set;} = DateTime.MinValue;
        public bool AutoPlayClip { get; set; } = false;

        public bool UseAi { get; set; } = true;
        public string AdditionalPrompt { get; set; } = "";

    }
}